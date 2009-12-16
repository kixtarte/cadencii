﻿/*
 * EditMode.cs
 * Copyright (c) 2008-2009 kbinani
 *
 * This file is part of org.kbinani.cadencii.
 *
 * org.kbinani.cadencii is free software; you can redistribute it and/or
 * modify it under the terms of the GPLv3 License.
 *
 * org.kbinani.cadencii is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 */
#if JAVA
package org.kbinani.cadencii;
#else
using System;

namespace org.kbinani.cadencii {
#endif

    /// <summary>
    /// ピアノロール画面の編集モード
    /// </summary>
    public enum EditMode {
        /// <summary>
        /// 何も編集して無い状態
        /// </summary>
        NONE,
        /// <summary>
        /// 真ん中ボタンでドラッグ中
        /// </summary>
        MIDDLE_DRAG,
        /// <summary>
        /// エントリを追加中
        /// </summary>
        ADD_ENTRY,
        /// <summary>
        /// エントリを移動中
        /// </summary>
        MOVE_ENTRY,
        /// <summary>
        /// エントリ移動に向け、マウスが動くのを待機中
        /// </summary>
        MOVE_ENTRY_WAIT_MOVE,
        /// <summary>
        /// コントロールカーブも同時移動するモードで、エントリを移動中
        /// </summary>
        MOVE_ENTRY_WHOLE,
        /// <summary>
        /// コントロールカーブも同時移動するモードで、マウスが動くのを待機中
        /// </summary>
        MOVE_ENTRY_WHOLE_WAIT_MOVE,
        /// <summary>
        /// エントリの左端(開始時刻)を編集中
        /// </summary>
        EDIT_LEFT_EDGE,
        /// <summary>
        /// エントリの右端(終了時刻)を編集中
        /// </summary>
        EDIT_RIGHT_EDGE,
        /// <summary>
        /// 固定長音符を追加
        /// </summary>
        ADD_FIXED_LENGTH_ENTRY,
        /// <summary>
        /// ビブラートの有効範囲を編集中
        /// </summary>
        EDIT_VIBRATO_DELAY,
        /// <summary>
        /// リアルタイム音符入力
        /// </summary>
        REALTIME,
    }

#if !JAVA
}
#endif
