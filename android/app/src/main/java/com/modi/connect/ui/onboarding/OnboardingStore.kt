package com.modi.connect.ui.onboarding

import android.content.Context

interface OnboardingPersistence {
    var terminalState: String?
}

class InMemoryOnboardingPersistence : OnboardingPersistence {
    override var terminalState: String? = null
}

class SharedPreferencesOnboardingPersistence(context: Context) : OnboardingPersistence {
    private val preferences = context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
    override var terminalState: String?
        get() = preferences.getString(KEY_TERMINAL_STATE, null)
        set(value) {
            if (value == null) preferences.edit().remove(KEY_TERMINAL_STATE).apply()
            else preferences.edit().putString(KEY_TERMINAL_STATE, value).apply()
        }

    companion object {
        private const val PREFERENCES = "modi_onboarding_v1"
        private const val KEY_TERMINAL_STATE = "terminal_state"
    }
}

class OnboardingStore(private val persistence: OnboardingPersistence) {
    fun shouldShow(): Boolean = persistence.terminalState == null
    fun complete() { persistence.terminalState = "complete" }
    fun skip() { persistence.terminalState = "skipped" }
    fun reset() { persistence.terminalState = null }
}
