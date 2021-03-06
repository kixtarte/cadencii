/*
 * PencilModeEnum.cs
 * Copyright © 2008-2011 kbinani
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
package com.github.cadencii;
#else
namespace com.github.cadencii {
#endif

    public enum PencilModeEnum {
        Off,
        L1,
        L2,
        L4,
        L8,
        L16,
        L32,
        L64,
        L128,
    }

#if !JAVA
}
#endif
