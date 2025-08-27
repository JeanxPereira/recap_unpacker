#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Registry Diff GUI (Qt6 / PySide6)

- Load a *registry* list and a *new names* list (one name per line)
- Preview additions, duplicates, similarity suggestions and a unified diff
- Apply changes safely (optional .bak backup, atomic writes, optional sort)
- Export the diff to a .patch file

Requires: PySide6 (Qt for Python 6)
"""

from __future__ import annotations

import sys
import os
import difflib
from dataclasses import dataclass
from collections import Counter
from pathlib import Path
from typing import Iterable

from PySide6.QtCore import Qt, QSettings, QStandardPaths
from PySide6.QtGui import QAction, QFont, QTextCharFormat, QColor, QSyntaxHighlighter, QPalette
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QFileDialog, QMessageBox, QWidget, QVBoxLayout,
    QHBoxLayout, QPlainTextEdit, QLabel, QPushButton, QListWidget, QTabWidget,
    QSplitter, QCheckBox, QSlider, QStatusBar
)
from PySide6.QtCore import QFile, QSaveFile, QTextStream


# ---------- Utilities ----------

def normalize_lines(text: str) -> list[str]:
    return [ln.strip() for ln in text.splitlines() if ln.strip()]


def read_names(path: str) -> list[str]:
    f = QFile(path)
    if not f.exists():
        raise FileNotFoundError(path)
    if not f.open(QFile.ReadOnly | QFile.Text):
        raise OSError(f"Cannot open file: {path}")
    stream = QTextStream(f)
    try:
        from PySide6.QtCore import QStringConverter
        stream.setEncoding(QStringConverter.Encoding.Utf8)
    except Exception:
        pass
    content = stream.readAll()
    f.close()
    return normalize_lines(content)


def write_names_atomic(path: str, names: Iterable[str], sort_output: bool = True) -> None:
    unique = []
    seen = set()
    for n in names:
        if n not in seen:
            seen.add(n)
            unique.append(n)
    if sort_output:
        unique.sort()
    data = "\n".join(unique) + "\n" if unique else ""

    saver = QSaveFile(path)
    if not saver.open(QFile.WriteOnly | QFile.Text):
        raise OSError(f"Cannot write file: {path}")
    stream = QTextStream(saver)
    try:
        from PySide6.QtCore import QStringConverter
        stream.setEncoding(QStringConverter.Encoding.Utf8)
    except Exception:
        pass
    stream << data
    if not saver.commit():
        raise OSError(f"Failed to commit file: {path}")


def find_similar(name: str, base: list[str], threshold: float = 0.8) -> list[str]:
    return [m for m in difflib.get_close_matches(name, base, n=3, cutoff=threshold) if m != name]


def deduplicate(items: list[str]) -> tuple[list[str], dict[str, int]]:
    c = Counter(items)
    dups = {k: v for k, v in c.items() if v > 1}
    return list(c.keys()), dups


# ---------- Diff Highlighter ----------

class UnifiedDiffHighlighter(QSyntaxHighlighter):
    def __init__(self, doc):
        super().__init__(doc)
        self.f_add = QTextCharFormat(); self.f_add.setForeground(QColor(120, 255, 120))
        self.f_del = QTextCharFormat(); self.f_del.setForeground(QColor(255, 120, 120))
        self.f_hunk = QTextCharFormat(); self.f_hunk.setForeground(QColor(180, 180, 255))
        self.f_head = QTextCharFormat(); self.f_head.setForeground(QColor(200, 200, 200))

    def highlightBlock(self, text: str) -> None:
        if text.startswith("+++") or text.startswith("---"):
            self.setFormat(0, len(text), self.f_head)
        elif text.startswith("@@"):
            self.setFormat(0, len(text), self.f_hunk)
        elif text.startswith("+"):
            self.setFormat(0, len(text), self.f_add)
        elif text.startswith("-"):
            self.setFormat(0, len(text), self.f_del)


# ---------- Core processing ----------

@dataclass
class Summary:
    to_add: list[str]
    duplicates: list[str]
    similar: dict[str, list[str]]
    input_dups: dict[str, int] | None


def compute_summary(registry: list[str], new_raw: list[str], *, dedup_input: bool, threshold: float) -> Summary:
    base = registry
    if dedup_input:
        new, input_dups = deduplicate(new_raw)
    else:
        new, input_dups = new_raw, None

    base_set = set(base)
    to_add, dup = [], []
    sim: dict[str, list[str]] = {}

    for n in new:
        if n in base_set:
            dup.append(n)
        else:
            to_add.append(n)
            matches = find_similar(n, base, threshold)
            if matches:
                sim[n] = matches

    return Summary(sorted(to_add), sorted(dup), sim, input_dups)


def unified_diff_text(before: list[str], after: list[str], *, from_name: str, to_name: str, context: int = 3) -> str:
    diff = difflib.unified_diff(before, after, fromfile=from_name, tofile=to_name, n=context)
    return "\n".join(diff)


# ---------- Main Window ----------

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Registry Diff GUI")
        self.resize(1200, 800)

        # Start all dialogs in the script directory
        self.base_dir = str(Path(__file__).resolve().parent)

        self.registry_path: str | None = None
        self.additions_path: str | None = None
        self.settings = QSettings("RegistryDiffGUI", "app")

        self._build_ui()
        self._build_menu()
        self._apply_dark_palette()
        self._restore_geometry()

    # UI
    def _build_ui(self) -> None:
        central = QWidget(); self.setCentralWidget(central)
        root = QVBoxLayout(central)

        # Options
        opts = QHBoxLayout()
        self.chk_backup = QCheckBox("Create .bak backup")
        self.chk_sort = QCheckBox("Sort on save"); self.chk_sort.setChecked(True)
        self.chk_dedup = QCheckBox("Deduplicate new names")
        self.sim_label = QLabel("Similarity: 0.80")
        self.sim_slider = QSlider(Qt.Horizontal)
        self.sim_slider.setRange(60, 95)
        self.sim_slider.setValue(80)
        self.sim_slider.valueChanged.connect(lambda v: self.sim_label.setText(f"Similarity: {v/100:.2f}"))
        self.btn_recalc = QPushButton("Recompute Diff"); self.btn_recalc.clicked.connect(self.recompute)
        self.btn_apply = QPushButton("Apply Changes"); self.btn_apply.clicked.connect(self.apply_changes)
        for w in (self.chk_backup, self.chk_sort, self.chk_dedup, self.sim_label, self.sim_slider, self.btn_recalc, self.btn_apply):
            opts.addWidget(w)
        opts.addStretch(1)
        root.addLayout(opts)

        splitter = QSplitter(Qt.Horizontal)
        left = QWidget(); left_layout = QVBoxLayout(left); left_layout.setContentsMargins(0,0,0,0)
        self.lbl_reg = QLabel("Registry (one per line)")
        self.txt_registry = QPlainTextEdit(); self.txt_registry.setLineWrapMode(QPlainTextEdit.NoWrap)
        self.txt_registry.setPlaceholderText("Load a registry file… or paste here")
        self.lbl_new = QLabel("New Names (one per line)")
        self.txt_new = QPlainTextEdit(); self.txt_new.setLineWrapMode(QPlainTextEdit.NoWrap)
        self.txt_new.setPlaceholderText("Load a names file… or paste here")
        left_layout.addWidget(self.lbl_reg)
        left_layout.addWidget(self.txt_registry, 1)
        left_layout.addWidget(self.lbl_new)
        left_layout.addWidget(self.txt_new, 1)

        right = QTabWidget()
        # Summary tab
        tab_summary = QWidget(); sum_layout = QVBoxLayout(tab_summary)
        self.list_add = QListWidget(); self.list_dup = QListWidget(); self.list_sim = QListWidget()
        sum_layout.addWidget(QLabel("To Add")); sum_layout.addWidget(self.list_add, 1)
        sum_layout.addWidget(QLabel("Duplicates (ignored)")); sum_layout.addWidget(self.list_dup, 1)
        sum_layout.addWidget(QLabel("Similarity suggestions")); sum_layout.addWidget(self.list_sim, 1)
        right.addTab(tab_summary, "Summary")
        # Diff tab
        tab_diff = QWidget(); diff_layout = QVBoxLayout(tab_diff)
        self.txt_diff = QPlainTextEdit(); self.txt_diff.setReadOnly(True); self.txt_diff.setLineWrapMode(QPlainTextEdit.NoWrap)
        self._diff_hl = UnifiedDiffHighlighter(self.txt_diff.document())
        diff_layout.addWidget(self.txt_diff)
        right.addTab(tab_diff, "Unified Diff")
        # Stats tab
        tab_stats = QWidget(); stats_layout = QVBoxLayout(tab_stats)
        self.lbl_stats = QLabel("–"); self.lbl_stats.setTextInteractionFlags(Qt.TextSelectableByMouse)
        stats_layout.addWidget(self.lbl_stats)
        right.addTab(tab_stats, "Statistics")

        splitter.addWidget(left); splitter.addWidget(right)
        splitter.setStretchFactor(0, 2); splitter.setStretchFactor(1, 3)
        root.addWidget(splitter, 1)

        self.status = QStatusBar(); self.setStatusBar(self.status)

    def _build_menu(self) -> None:
        mb = self.menuBar(); file_menu = mb.addMenu("File")
        act_open_reg = QAction("Open Registry…", self); act_open_reg.triggered.connect(self.open_registry)
        act_open_new = QAction("Open New Names…", self); act_open_new.triggered.connect(self.open_new)
        act_export = QAction("Export Diff…", self); act_export.triggered.connect(self.export_diff)
        act_apply = QAction("Apply Changes", self); act_apply.setShortcut("Ctrl+S"); act_apply.triggered.connect(self.apply_changes)
        act_quit = QAction("Quit", self); act_quit.triggered.connect(self.close)
        for a in (act_open_reg, act_open_new, None, act_export, None, act_apply, None, act_quit):
            file_menu.addSeparator() if a is None else file_menu.addAction(a)

    def _apply_dark_palette(self) -> None:
        QApplication.setStyle("Fusion")
        pal = QPalette()
        pal.setColor(QPalette.ColorRole.Window, QColor(33, 37, 43))
        pal.setColor(QPalette.ColorRole.Base, QColor(26, 29, 34))
        pal.setColor(QPalette.ColorRole.AlternateBase, QColor(44, 49, 56))
        pal.setColor(QPalette.ColorRole.Text, QColor(230, 230, 230))
        pal.setColor(QPalette.ColorRole.Button, QColor(44, 49, 56))
        pal.setColor(QPalette.ColorRole.ButtonText, QColor(230, 230, 230))
        pal.setColor(QPalette.ColorRole.Highlight, QColor(42, 130, 218))
        pal.setColor(QPalette.ColorRole.HighlightedText, QColor(255, 255, 255))
        pal.setColor(QPalette.ColorRole.Link, QColor(90, 170, 250))
        self.setPalette(pal)

    # Settings
    def _restore_geometry(self):
        if geo := self.settings.value("main/geometry"):
            self.restoreGeometry(geo)

    def closeEvent(self, e):
        self.settings.setValue("main/geometry", self.saveGeometry())
        super().closeEvent(e)

    # Actions
    def open_registry(self) -> None:
        path = self._open_file("Open Registry", "Text files (*.txt);;All files (*.*)")
        if not path:
            return
        try:
            names = read_names(path)
        except Exception as ex:
            QMessageBox.critical(self, "Error", str(ex)); return
        self.registry_path = path
        self.txt_registry.setPlainText("\n".join(names))
        self._update_labels()
        self.recompute()

    def open_new(self) -> None:
        path = self._open_file("Open New Names", "Text files (*.txt);;All files (*.*)")
        if not path:
            return
        try:
            names = read_names(path)
        except Exception as ex:
            QMessageBox.critical(self, "Error", str(ex)); return
        self.additions_path = path
        self.txt_new.setPlainText("\n".join(names))
        self._update_labels()
        self.recompute()

    def export_diff(self) -> None:
        if not self.txt_diff.toPlainText().strip():
            QMessageBox.information(self, "Export Diff", "Nothing to export."); return
        path = self._save_file("Export Diff", "changes.patch", "Patch files (*.patch *.diff);;All files (*.*)")
        if not path:
            return
        try:
            write_names_atomic(path, self.txt_diff.toPlainText().splitlines(False), sort_output=False)
        except Exception as ex:
            QMessageBox.critical(self, "Error", str(ex)); return
        self.status.showMessage(f"Diff exported: {path}", 5000)

    def apply_changes(self) -> None:
        reg = normalize_lines(self.txt_registry.toPlainText())
        new = normalize_lines(self.txt_new.toPlainText())
        if not reg and not new:
            QMessageBox.information(self, "Apply", "Nothing to apply."); return
        s = self._summary(reg, new)
        total_before = len(reg)
        updated = sorted(set(reg) | set(s.to_add)) if self.chk_sort.isChecked() else list(dict.fromkeys(reg + s.to_add))

        if self.registry_path is None:
            def_name = "registry.txt"
            dest = self._save_file("Save Registry As", def_name, "Text files (*.txt);;All files (*.*)")
            if not dest:
                return
            self.registry_path = dest

        if self.chk_backup.isChecked() and self.registry_path and os.path.exists(self.registry_path):
            backup = self.registry_path + ".bak"
            try:
                write_names_atomic(backup, reg, sort_output=self.chk_sort.isChecked())
            except Exception as ex:
                QMessageBox.warning(self, "Backup", f"Failed to create backup: {ex}")

        try:
            write_names_atomic(self.registry_path, updated, sort_output=False)
        except Exception as ex:
            QMessageBox.critical(self, "Error", str(ex)); return

        self.txt_registry.setPlainText("\n".join(updated))
        self._update_labels()
        self.recompute()
        QMessageBox.information(self, "Done", (
            f"Original: {total_before}\n"
            f"Added: {len(s.to_add)}\n"
            f"Duplicates ignored: {len(s.duplicates)}\n"
            f"Updated total: {len(updated)}"
        ))

    # Helpers
    def _open_file(self, title: str, name_filter: str) -> str:
        dlg = QFileDialog(self, title)
        dlg.setFileMode(QFileDialog.ExistingFile)
        dlg.setNameFilter(name_filter)
        dlg.setDirectory(self.base_dir)
        dlg.setOption(QFileDialog.DontUseNativeDialog, True)
        if dlg.exec():
            files = dlg.selectedFiles()
            return files[0] if files else ""
        return ""

    def _save_file(self, title: str, default_name: str, name_filter: str) -> str:
        dlg = QFileDialog(self, title)
        dlg.setAcceptMode(QFileDialog.AcceptSave)
        dlg.setNameFilter(name_filter)
        dlg.setDirectory(self.base_dir)
        dlg.selectFile(default_name)
        dlg.setOption(QFileDialog.DontUseNativeDialog, True)
        if dlg.exec():
            files = dlg.selectedFiles()
            return files[0] if files else ""
        return ""

    # Helpers (cont.)
    def _start_dir(self) -> str:
        d = self.settings.value("paths/lastDir")
        if d and os.path.isdir(d):
            return d
        candidates = QStandardPaths.standardLocations(QStandardPaths.DocumentsLocation)
        return candidates[0] if candidates else os.getcwd()

    def _update_labels(self) -> None:
        reg_names = normalize_lines(self.txt_registry.toPlainText())
        new_names = normalize_lines(self.txt_new.toPlainText())
        reg_title = Path(self.registry_path).name if self.registry_path else "(unsaved)"
        new_title = Path(self.additions_path).name if self.additions_path else "(clipboard)"
        self.lbl_reg.setText(f"Registry: {reg_title} ({len(reg_names)} names)")
        self.lbl_new.setText(f"New Names: {new_title} ({len(new_names)} names)")
        if self.registry_path:
            pass
    def _summary(self, registry: list[str], new_raw: list[str]) -> Summary:
        threshold = self.sim_slider.value() / 100.0
        return compute_summary(registry, new_raw, dedup_input=self.chk_dedup.isChecked(), threshold=threshold)

    def recompute(self) -> None:
        reg = normalize_lines(self.txt_registry.toPlainText())
        new = normalize_lines(self.txt_new.toPlainText())
        s = self._summary(reg, new)

        self.list_add.clear(); self.list_dup.clear(); self.list_sim.clear()
        for n in s.to_add: self.list_add.addItem(n)
        for n in s.duplicates: self.list_dup.addItem(n)
        for k, v in sorted(s.similar.items()): self.list_sim.addItem(f"{k}  ~  {', '.join(v)}")

        updated_sorted = sorted(set(reg) | set(s.to_add))
        diff = unified_diff_text(
            reg,
            updated_sorted,
            from_name=(Path(self.registry_path).name if self.registry_path else "registry.txt"),
            to_name="registry(updated).txt",
            context=3,
        )
        self.txt_diff.setPlainText(diff)

        stats = [
            f"Registry names: {len(reg)}",
            f"New names (raw): {len(new)}",
            f"Input duplicates removed: {sum(v-1 for v in (s.input_dups or {}).values()) if s.input_dups else 0}",
            f"To add: {len(s.to_add)}",
            f"Duplicates ignored: {len(s.duplicates)}",
            f"Similarity hints: {len(s.similar)}",
            f"Updated total (sorted unique): {len(updated_sorted)}",
        ]
        self.lbl_stats.setText("\n".join(stats))
        self.status.showMessage("Diff recomputed", 3000)


def main() -> int:
    app = QApplication(sys.argv)
    w = MainWindow()
    w.show()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
