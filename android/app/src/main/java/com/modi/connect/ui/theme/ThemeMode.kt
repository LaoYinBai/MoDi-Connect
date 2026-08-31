package com.modi.connect.ui.theme

enum class ThemeMode(val storedValue: String, val label: String) {
    INK("ink", "墨堤"), PAPER("paper", "昼堤"), SYSTEM("system", "跟随系统");

    fun isDark(systemDark: Boolean): Boolean = when (this) {
        INK -> true
        PAPER -> false
        SYSTEM -> systemDark
    }

    companion object {
        fun fromStored(value: String?): ThemeMode = entries.firstOrNull { it.storedValue == value } ?: SYSTEM
    }
}
