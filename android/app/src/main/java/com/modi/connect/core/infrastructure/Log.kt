/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
package com.modi.connect.core.infrastructure

import com.modi.connect.core.impl.LogcatLogger
import com.modi.connect.core.interfaces.ILogger

object Log {
    private var _impl: ILogger = LogcatLogger()

    fun setImpl(impl: ILogger) { _impl = impl }

    fun d(tag: String, msg: String) = _impl.debug(tag, msg)
    fun i(tag: String, msg: String) = _impl.info(tag, msg)
    fun w(tag: String, msg: String) = _impl.warn(tag, msg)
    fun e(tag: String, msg: String) = _impl.error(tag, msg)
    fun e(tag: String, msg: String, ex: Exception) = _impl.error(tag, msg, ex)
}
