import ctypes
import json
import os
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import font as tkfont

__version__ = "1.0.0"

# Enable DPI awareness on Windows for crisp rendering
if sys.platform == "win32":
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(1)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass

APP_DIR = os.path.dirname(os.path.abspath(__file__))
SENSOR_EXE = os.path.join(APP_DIR, "lib", "sensor_reader.exe")
CONFIG_PATH = os.path.join(APP_DIR, "config.json")

TRANSPARENT_KEY = "#010101"
BG_CARD = "#0D0F14"
BORDER_CARD = "#262C3A"
BG_SECTION = "#171B24"
BORDER_SECTION = "#2B3244"
TRACK_BG = "#232836"
TEXT_MUTED = "#7B8498"
TEXT_SUB = "#CBD5E1"
ACCENT_CPU = "#60A5FA"
ACCENT_GPU = "#C084FC"
ACCENT_RAM = "#2DD4BF"

DEFAULT_CONFIG = {
    "corner": "top-right",
    "x": None,
    "y": None,
    "alpha": 0.92,
    "compact": False,
    "topmost": True,
}


def is_admin() -> bool:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def relaunch_as_admin():
    """Relaunch this script with UAC elevation so CPU temperature sensors are accessible."""
    script = os.path.abspath(__file__)
    exe = sys.executable
    # Prefer pythonw.exe so no console window appears
    if exe.lower().endswith("python.exe"):
        pyw = exe[:-10] + "pythonw.exe"
        if os.path.exists(pyw):
            exe = pyw
    params = f'"{script}"'
    ctypes.windll.shell32.ShellExecuteW(None, "runas", exe, params, APP_DIR, 1)


def temp_color(temp):
    if temp is None:
        return "#64748B"
    if temp < 60:
        return "#4ADE80"  # Green
    if temp < 75:
        return "#FACC15"  # Yellow
    if temp < 85:
        return "#FB923C"  # Orange
    return "#F87171"      # Red


def usage_color(pct):
    if pct is None:
        return "#64748B"
    if pct < 70:
        return "#4ADE80"  # Green
    if pct < 85:
        return "#FACC15"  # Yellow
    if pct < 95:
        return "#FB923C"  # Orange
    return "#F87171"      # Red


class _MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [
        ("dwLength", ctypes.c_ulong),
        ("dwMemoryLoad", ctypes.c_ulong),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


def get_ram_info():
    """Return (percent, used_gb, total_gb) via Windows GlobalMemoryStatusEx."""
    try:
        stat = _MEMORYSTATUSEX()
        stat.dwLength = ctypes.sizeof(_MEMORYSTATUSEX)
        if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(stat)):
            total_gb = stat.ullTotalPhys / (1024 ** 3)
            used_gb = (stat.ullTotalPhys - stat.ullAvailPhys) / (1024 ** 3)
            pct = float(stat.dwMemoryLoad)
            return (pct, used_gb, total_gb)
    except Exception:
        pass
    return (None, None, None)


def load_config():
    cfg = dict(DEFAULT_CONFIG)
    try:
        if os.path.exists(CONFIG_PATH):
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, dict):
                    cfg.update(data)
    except Exception:
        pass
    return cfg


def save_config(cfg):
    try:
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(cfg, f, indent=2)
    except Exception:
        pass


def draw_rounded_rect(canvas, x1, y1, x2, y2, r, **kwargs):
    points = [
        x1 + r, y1,
        x2 - r, y1,
        x2, y1,
        x2, y1 + r,
        x2, y2 - r,
        x2, y2,
        x2 - r, y2,
        x1 + r, y2,
        x1, y2,
        x1, y2 - r,
        x1, y1 + r,
        x1, y1,
    ]
    return canvas.create_polygon(points, smooth=True, splinesteps=24, **kwargs)


class SensorWorker:
    """Runs lib/sensor_reader.exe hidden in the background and streams JSON readings."""

    def __init__(self, on_data):
        self.on_data = on_data
        self.proc = None
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True)

    def start(self):
        self._thread.start()

    def stop(self):
        self._stop.set()
        proc = self.proc
        if proc is not None:
            try:
                proc.terminate()
            except Exception:
                pass
            try:
                proc.wait(timeout=1.5)
            except Exception:
                try:
                    proc.kill()
                except Exception:
                    pass

    def _run(self):
        exe_path = SENSOR_EXE
        staged_exe = os.path.join(os.path.dirname(SENSOR_EXE), "sensor_reader_new.exe")
        if os.path.exists(staged_exe):
            try:
                os.replace(staged_exe, SENSOR_EXE)
            except OSError:
                exe_path = staged_exe

        if not os.path.exists(exe_path):
            self.on_data({"error": "sensor_reader.exe not found"})
            return

        creationflags = 0x08000000 if sys.platform == "win32" else 0  # CREATE_NO_WINDOW
        while not self._stop.is_set():
            try:
                self.proc = subprocess.Popen(
                    [exe_path, "--interval", "1000"],
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                    encoding="utf-8",
                    cwd=os.path.dirname(exe_path),
                    creationflags=creationflags,
                )
                assert self.proc.stdout is not None
                for line in self.proc.stdout:
                    if self._stop.is_set():
                        break
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        data = json.loads(line)
                        self.on_data(data)
                    except json.JSONDecodeError:
                        continue
            except Exception as exc:
                self.on_data({"error": str(exc)})

            if not self._stop.is_set():
                self._stop.wait(2.0)


class TempMonitorApp:
    def __init__(self):
        self.cfg = load_config()
        self.admin = is_admin()
        self.data = {
            "cpu_temp": None,
            "cpu_load": None,
            "gpu_temp": None,
            "gpu_load": None,
            "gpu_vram_used_mb": None,
            "gpu_vram_total_mb": None,
            "cpu_name": "CPU",
            "gpu_name": "GPU",
            "is_admin": self.admin,
        }
        self._lock = threading.Lock()
        self._drag_x = 0
        self._drag_y = 0
        self._dragging = False
        self._close_hover = False
        self._menu_hover = False
        self._menu_open = False

        self.root = tk.Tk()
        self.root.title("PC Vitals Widget")
        self.root.overrideredirect(True)
        self.root.configure(bg=TRANSPARENT_KEY)
        self.root.attributes("-transparentcolor", TRANSPARENT_KEY)
        self.root.attributes("-topmost", bool(self.cfg.get("topmost", True)))
        self.root.attributes("-alpha", float(self.cfg.get("alpha", 0.92)))
        try:
            self.root.attributes("-toolwindow", True)
        except Exception:
            pass

        # Fonts
        self.font_header = tkfont.Font(family="Segoe UI", size=8, weight="bold")
        self.font_label = tkfont.Font(family="Segoe UI", size=9, weight="bold")
        self.font_sub = tkfont.Font(family="Segoe UI", size=8)
        self.font_val = tkfont.Font(family="Consolas", size=11, weight="bold")
        self.font_temp = tkfont.Font(family="Consolas", size=15, weight="bold")
        self.font_temp_compact = tkfont.Font(family="Consolas", size=13, weight="bold")
        self.font_hint = tkfont.Font(family="Segoe UI", size=7, underline=True)
        self.font_icon = tkfont.Font(family="Segoe UI", size=9, weight="bold")

        self.canvas = tk.Canvas(
            self.root,
            bg=TRANSPARENT_KEY,
            highlightthickness=0,
            bd=0,
        )
        self.canvas.pack(fill="both", expand=True)

        # Mouse bindings
        self.canvas.bind("<ButtonPress-1>", self._on_press)
        self.canvas.bind("<B1-Motion>", self._on_drag)
        self.canvas.bind("<ButtonRelease-1>", self._on_release)
        self.canvas.bind("<Double-Button-1>", self._on_double_click)
        self.canvas.bind("<Button-3>", self._show_menu)
        self.canvas.bind("<Motion>", self._on_motion)
        self.canvas.bind("<Leave>", self._on_leave)
        self.root.bind("<Escape>", lambda e: self.close())

        self._build_menu()
        self._apply_size_and_position(initial=True)
        self._redraw()

        # Start sensor worker
        self.worker = SensorWorker(self._on_sensor_data)
        self.worker.start()

        # Periodic UI refresh & topmost guard
        self.root.after(250, self._tick)
        self.root.protocol("WM_DELETE_WINDOW", self.close)

    def _dimensions(self):
        compact = bool(self.cfg.get("compact", False))
        if compact:
            return (174, 118)
        needs_admin_hint = (not self.admin) and (self.data.get("cpu_temp") is None)
        return (196, 280 if needs_admin_hint else 264)

    def _apply_size_and_position(self, initial=False):
        w, h = self._dimensions()
        sw = self.root.winfo_screenwidth()
        sh = self.root.winfo_screenheight()
        margin = 14
        taskbar_pad = 52

        if initial and self.cfg.get("x") is not None and self.cfg.get("y") is not None:
            x = max(0, min(int(self.cfg["x"]), sw - w))
            y = max(0, min(int(self.cfg["y"]), sh - h))
        elif not initial and self.cfg.get("corner") is None and self.cfg.get("x") is not None:
            x = max(0, min(self.root.winfo_x(), sw - w))
            y = max(0, min(self.root.winfo_y(), sh - h))
        else:
            corner = self.cfg.get("corner") or "top-right"
            if corner == "top-left":
                x, y = margin, margin
            elif corner == "bottom-left":
                x, y = margin, sh - h - taskbar_pad
            elif corner == "bottom-right":
                x, y = sw - w - margin, sh - h - taskbar_pad
            else:  # top-right
                x, y = sw - w - margin, margin

        self.root.geometry(f"{w}x{h}+{x}+{y}")
        self.canvas.config(width=w, height=h)

    def _build_menu(self):
        self.menu = tk.Menu(
            self.root,
            tearoff=0,
            bg="#181B22",
            fg="#E2E8F0",
            activebackground="#2D3548",
            activeforeground="#FFFFFF",
            bd=0,
        )

        # Corner submenu
        pos_menu = tk.Menu(
            self.menu,
            tearoff=0,
            bg="#181B22",
            fg="#E2E8F0",
            activebackground="#2D3548",
            activeforeground="#FFFFFF",
        )
        for label, key in [
            ("Top Right", "top-right"),
            ("Top Left", "top-left"),
            ("Bottom Right", "bottom-right"),
            ("Bottom Left", "bottom-left"),
        ]:
            pos_menu.add_command(label=label, command=lambda k=key: self._set_corner(k))
        self.menu.add_cascade(label="Screen Corner", menu=pos_menu)

        # Opacity submenu
        alpha_menu = tk.Menu(
            self.menu,
            tearoff=0,
            bg="#181B22",
            fg="#E2E8F0",
            activebackground="#2D3548",
            activeforeground="#FFFFFF",
        )
        for pct in (100, 92, 80, 65):
            alpha_menu.add_command(
                label=f"{pct}%",
                command=lambda a=pct / 100.0: self._set_alpha(a),
            )
        self.menu.add_cascade(label="Opacity", menu=alpha_menu)

        self.compact_var = tk.BooleanVar(value=bool(self.cfg.get("compact", False)))
        self.topmost_var = tk.BooleanVar(value=bool(self.cfg.get("topmost", True)))

        self.menu.add_separator()
        self.menu.add_checkbutton(
            label="Compact Mode",
            variable=self.compact_var,
            command=self._toggle_compact,
        )
        self.menu.add_checkbutton(
            label="Always on Top",
            variable=self.topmost_var,
            command=self._toggle_topmost,
        )

        if not self.admin:
            self.menu.add_separator()
            self.menu.add_command(
                label="Run as Admin (CPU Sensor)",
                command=self._elevate,
            )

        self.menu.add_separator()
        self.menu.add_command(label="Close", command=self.close)

    def _set_corner(self, corner):
        self.cfg["corner"] = corner
        self.cfg["x"] = None
        self.cfg["y"] = None
        save_config(self.cfg)
        self._apply_size_and_position()
        self._redraw()

    def _set_alpha(self, alpha):
        self.cfg["alpha"] = alpha
        self.root.attributes("-alpha", alpha)
        save_config(self.cfg)

    def _toggle_compact(self):
        self.cfg["compact"] = bool(self.compact_var.get())
        save_config(self.cfg)
        self._apply_size_and_position()
        self._redraw()

    def _toggle_topmost(self):
        val = bool(self.topmost_var.get())
        self.cfg["topmost"] = val
        self.root.attributes("-topmost", val)
        save_config(self.cfg)

    def _elevate(self):
        self.worker.stop()
        relaunch_as_admin()
        self.root.destroy()

    def _show_menu(self, event):
        self._menu_open = True
        try:
            self.root.attributes("-topmost", False)
            self.root.lift()
            self.root.update_idletasks()
            self.menu.tk_popup(event.x_root, event.y_root)
        finally:
            self._menu_open = False
            try:
                self.menu.grab_release()
                if self.root.winfo_exists():
                    self.root.attributes("-topmost", bool(self.cfg.get("topmost", True)))
            except Exception:
                pass

    def _hit_close(self, x, y):
        w, _ = self._dimensions()
        return (w - 24 <= x <= w - 4) and (4 <= y <= 24)

    def _hit_menu(self, x, y):
        w, _ = self._dimensions()
        return (w - 42 <= x <= w - 24) and (4 <= y <= 24)

    def _hit_admin_hint(self, x, y):
        if self.admin or self.cfg.get("compact") or self.data.get("cpu_temp") is not None:
            return False
        w, h = self._dimensions()
        return (10 <= x <= w - 10) and (h - 24 <= y <= h - 4)

    def _on_motion(self, event):
        ch = self._hit_close(event.x, event.y)
        mh = self._hit_menu(event.x, event.y)
        ah = self._hit_admin_hint(event.x, event.y)
        if ch or mh or ah:
            self.canvas.config(cursor="hand2")
        else:
            self.canvas.config(cursor="fleur")
        if ch != self._close_hover or mh != self._menu_hover:
            self._close_hover = ch
            self._menu_hover = mh
            self._redraw()

    def _on_leave(self, _event):
        if self._close_hover or self._menu_hover:
            self._close_hover = False
            self._menu_hover = False
            self._redraw()

    def _on_press(self, event):
        if self._hit_close(event.x, event.y):
            self.close()
            return
        if self._hit_menu(event.x, event.y):
            self._show_menu(event)
            return
        if self._hit_admin_hint(event.x, event.y):
            self._elevate()
            return
        self._dragging = True
        self._drag_x = event.x
        self._drag_y = event.y

    def _on_drag(self, event):
        if not self._dragging:
            return
        nx = self.root.winfo_x() + (event.x - self._drag_x)
        ny = self.root.winfo_y() + (event.y - self._drag_y)
        self.root.geometry(f"+{nx}+{ny}")

    def _on_release(self, _event):
        if self._dragging:
            self._dragging = False
            self.cfg["corner"] = None
            self.cfg["x"] = self.root.winfo_x()
            self.cfg["y"] = self.root.winfo_y()
            save_config(self.cfg)

    def _on_double_click(self, event):
        if self._hit_close(event.x, event.y) or self._hit_menu(event.x, event.y):
            return
        self.compact_var.set(not self.compact_var.get())
        self._toggle_compact()

    def _on_sensor_data(self, payload):
        with self._lock:
            self.data.update(payload)

    def _tick(self):
        self._redraw()
        if self.cfg.get("topmost", True) and not self._menu_open:
            try:
                self.root.attributes("-topmost", True)
            except Exception:
                pass
        self.root.after(1000, self._tick)

    def _redraw(self):
        with self._lock:
            d = dict(self.data)

        cpu_temp = d.get("cpu_temp")
        cpu_load = d.get("cpu_load")
        gpu_temp = d.get("gpu_temp")
        gpu_load = d.get("gpu_load")
        vram_used_mb = d.get("gpu_vram_used_mb")
        vram_total_mb = d.get("gpu_vram_total_mb")
        vram_used_gb = (vram_used_mb / 1024.0) if vram_used_mb is not None else None
        vram_total_gb = (vram_total_mb / 1024.0) if vram_total_mb is not None else None
        vram_pct = (
            min(100.0, max(0.0, (vram_used_mb / vram_total_mb) * 100.0))
            if (vram_used_mb is not None and vram_total_mb is not None and vram_total_mb > 0)
            else None
        )
        ram_pct, ram_used, ram_total = get_ram_info()

        # Adjust height dynamically if admin hint appears/disappears
        w, h = self._dimensions()
        if int(self.canvas.cget("height")) != h or int(self.canvas.cget("width")) != w:
            self._apply_size_and_position()

        c = self.canvas
        c.delete("all")

        # Outer rounded card
        draw_rounded_rect(c, 1, 1, w - 1, h - 1, 12, fill=BG_CARD, outline=BORDER_CARD, width=1)

        # Status dot color (highest available temp)
        temps = [t for t in (cpu_temp, gpu_temp) if t is not None]
        status_col = temp_color(max(temps)) if temps else "#64748B"

        compact = bool(self.cfg.get("compact", False))

        if compact:
            # Tiny top-right close button on hover
            c.create_oval(10, 10, 16, 16, fill=status_col, outline="")
            c.create_text(
                w - 13, 13, text="×",
                fill="#F87171" if self._close_hover else "#475569",
                font=self.font_icon,
            )

            # CPU grouped: label | uso% | temp°C
            cpu_t = f"{cpu_temp:.0f}°C" if cpu_temp is not None else "--°C"
            cpu_u = f"{cpu_load:.0f}%" if cpu_load is not None else "--%"
            c.create_text(13, 34, text="CPU", anchor="w", fill=ACCENT_CPU, font=self.font_label)
            c.create_text(58, 34, text=cpu_u, anchor="w", fill=usage_color(cpu_load), font=self.font_sub)
            c.create_text(w - 13, 34, text=cpu_t, anchor="e", fill=temp_color(cpu_temp), font=self.font_temp_compact)

            # GPU grouped: label | uso% | temp°C
            gpu_t = f"{gpu_temp:.0f}°C" if gpu_temp is not None else "--°C"
            gpu_u = f"{gpu_load:.0f}%" if gpu_load is not None else "--%"
            c.create_text(13, 56, text="GPU", anchor="w", fill=ACCENT_GPU, font=self.font_label)
            c.create_text(58, 56, text=gpu_u, anchor="w", fill=usage_color(gpu_load), font=self.font_sub)
            c.create_text(w - 13, 56, text=gpu_t, anchor="e", fill=temp_color(gpu_temp), font=self.font_temp_compact)

            # VRAM: label | GB | uso%
            vram_u = f"{vram_pct:.0f}%" if vram_pct is not None else "--%"
            vram_gb = f"{vram_used_gb:.1f}G" if vram_used_gb is not None else ""
            c.create_text(13, 78, text="VRAM", anchor="w", fill=ACCENT_GPU, font=self.font_sub)
            c.create_text(58, 78, text=vram_gb, anchor="w", fill=TEXT_SUB, font=self.font_sub)
            c.create_text(w - 13, 78, text=vram_u, anchor="e", fill=usage_color(vram_pct), font=self.font_temp_compact)

            # RAM: label | GB | uso%
            ram_u = f"{ram_pct:.0f}%" if ram_pct is not None else "--%"
            ram_gb = f"{ram_used:.1f}G" if ram_used is not None else ""
            c.create_text(13, 100, text="RAM", anchor="w", fill=ACCENT_RAM, font=self.font_label)
            c.create_text(58, 100, text=ram_gb, anchor="w", fill=TEXT_SUB, font=self.font_sub)
            c.create_text(w - 13, 100, text=ram_u, anchor="e", fill=usage_color(ram_pct), font=self.font_temp_compact)
            return

        # --- NORMAL MODE (Blocos separados: CPU, GPU e RAM em sub-cards) ---
        # Header
        c.create_oval(12, 11, 19, 18, fill=status_col, outline="")
        c.create_text(25, 14, text="MONITOR", anchor="w", fill=TEXT_MUTED, font=self.font_header)

        # Menu icon (⋮) & Close icon (×)
        c.create_text(
            w - 31, 13, text="⋮",
            fill="#E2E8F0" if self._menu_hover else "#525C70",
            font=self.font_icon,
        )
        c.create_text(
            w - 14, 13, text="×",
            fill="#F87171" if self._close_hover else "#525C70",
            font=self.font_icon,
        )

        # 1. CPU Sub-card (y = 30..104)
        self._draw_component_group(
            c, y_top=30, width=w,
            title="CPU", accent=ACCENT_CPU,
            temp=cpu_temp, load=cpu_load,
        )

        # 2. GPU Sub-card (y = 111..211)
        vram_sub = f"{vram_used_gb:.1f}/{vram_total_gb:.0f}G" if (vram_used_gb is not None and vram_total_gb is not None) else ""
        self._draw_component_group(
            c, y_top=111, width=w,
            title="GPU", accent=ACCENT_GPU,
            temp=gpu_temp, load=gpu_load,
            vram_pct=vram_pct, vram_sub=vram_sub,
        )

        # 3. RAM Sub-card (y = 218..256)
        ram_sub = f"{ram_used:.1f}/{ram_total:.0f}G" if (ram_used is not None and ram_total is not None) else ""
        self._draw_ram_group(
            c, y_top=218, width=w,
            sub=ram_sub, pct=ram_pct,
        )

        # Admin hint if CPU temp is unavailable because not elevated
        if (not self.admin) and (cpu_temp is None):
            c.create_text(
                w // 2, h - 12,
                text="⚡ Enable CPU Sensor (Admin)",
                fill="#FBBF24",
                font=self.font_hint,
            )

    def _draw_section_box(self, c, x1, y1, x2, y2, accent):
        draw_rounded_rect(c, x1, y1, x2, y2, 8, fill=BG_SECTION, outline=BORDER_SECTION, width=1)
        # Colored left accent pill inside the section card
        draw_rounded_rect(c, x1 + 2, y1 + 7, x1 + 5, y2 - 7, 2, fill=accent, outline="")

    def _draw_mini_bar(self, c, x1, y1, x2, y2, value, color):
        draw_rounded_rect(c, x1, y1, x2, y2, 2, fill=TRACK_BG, outline="")
        if value is not None and value > 0:
            ratio = max(0.05, min(1.0, value / 100.0))
            fill_x2 = x1 + int((x2 - x1) * ratio)
            if fill_x2 > x1 + 4:
                draw_rounded_rect(c, x1, y1, fill_x2, y2, 2, fill=color, outline="")

    def _draw_component_group(self, c, y_top, width, title, accent, temp, load, vram_pct=None, vram_sub=None):
        has_vram = vram_sub is not None
        box_h = 100 if has_vram else 74
        self._draw_section_box(c, 8, y_top, width - 8, y_top + box_h, accent)

        pad_l = 18
        pad_r = width - 16

        # Component title
        c.create_text(pad_l, y_top + 11, text=title, anchor="w", fill=accent, font=self.font_label)

        # Sub-row 1: Temp
        t_str = f"{temp:.0f}°C" if temp is not None else "--°C"
        t_col = temp_color(temp)
        c.create_text(pad_l, y_top + 27, text="Temp", anchor="w", fill=TEXT_MUTED, font=self.font_sub)
        c.create_text(pad_r, y_top + 27, text=t_str, anchor="e", fill=t_col, font=self.font_val)
        self._draw_mini_bar(c, pad_l, y_top + 36, pad_r, y_top + 40, temp, t_col)

        # Sub-row 2: Load
        u_str = f"{load:.0f}%" if load is not None else "--%"
        u_col = usage_color(load)
        c.create_text(pad_l, y_top + 53, text="Load", anchor="w", fill=TEXT_MUTED, font=self.font_sub)
        c.create_text(pad_r, y_top + 53, text=u_str, anchor="e", fill=u_col, font=self.font_val)
        self._draw_mini_bar(c, pad_l, y_top + 62, pad_r, y_top + 66, load, u_col)

        # Optional Sub-row 3: VRAM (for GPU)
        if has_vram:
            v_str = f"{vram_pct:.0f}%" if vram_pct is not None else "--%"
            v_col = usage_color(vram_pct)
            c.create_text(pad_l, y_top + 79, text="VRAM", anchor="w", fill=TEXT_MUTED, font=self.font_sub)
            if vram_sub:
                c.create_text(pad_l + 36, y_top + 79, text=vram_sub, anchor="w", fill=TEXT_SUB, font=self.font_sub)
            c.create_text(pad_r, y_top + 79, text=v_str, anchor="e", fill=v_col, font=self.font_val)
            self._draw_mini_bar(c, pad_l, y_top + 88, pad_r, y_top + 92, vram_pct, v_col)

    def _draw_ram_group(self, c, y_top, width, sub, pct):
        self._draw_section_box(c, 8, y_top, width - 8, y_top + 38, ACCENT_RAM)

        pad_l = 18
        pad_r = width - 16
        c.create_text(pad_l, y_top + 13, text="RAM", anchor="w", fill=ACCENT_RAM, font=self.font_label)
        if sub:
            c.create_text(pad_l + 36, y_top + 13, text=sub, anchor="w", fill=TEXT_SUB, font=self.font_sub)

        p_str = f"{pct:.0f}%" if pct is not None else "--%"
        p_col = usage_color(pct)
        c.create_text(pad_r, y_top + 13, text=p_str, anchor="e", fill=p_col, font=self.font_val)
        self._draw_mini_bar(c, pad_l, y_top + 25, pad_r, y_top + 29, pct, p_col)

    def close(self):
        self.worker.stop()
        try:
            self.root.destroy()
        except Exception:
            pass

    def run(self):
        self.root.mainloop()


if __name__ == "__main__":
    app = TempMonitorApp()
    app.run()
