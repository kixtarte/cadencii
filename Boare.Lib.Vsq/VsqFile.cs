﻿/*
 * VsqFile.cs
 * Copyright (C) 2008-2009 kbinani
 *
 * This file is part of org.kbinani.vsq.
 *
 * org.kbinani.vsq is free software; you can redistribute it and/or
 * modify it under the terms of the BSD License.
 *
 * org.kbinani.vsq is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 */
#define NEW_IMPL
#if JAVA
package org.kbinani.vsq;

import java.util.*;
import java.io.*;
import org.kbinani.*;
#else
using System;
using org.kbinani;
using org.kbinani.java.util;
using org.kbinani.java.io;

namespace org.kbinani.vsq {
    using boolean = System.Boolean;
    using Integer = Int32;
    using Long = System.Int64;
#endif

    /// <summary>
    /// VSQファイルの内容を保持するクラス
    /// </summary>
#if JAVA
    public class VsqFile implements Cloneable, Serializable{
#else
    [Serializable]
    public class VsqFile : ICloneable {
#endif
        /// <summary>
        /// トラックのリスト．最初のトラックはMasterTrackであり，通常の音符が格納されるトラックはインデックス1以降となる
        /// </summary>
        public Vector<VsqTrack> Track;
        /// <summary>
        /// テンポ情報を保持したテーブル
        /// </summary>
        public Vector<TempoTableEntry> TempoTable;
        public Vector<TimeSigTableEntry> TimesigTable;
        protected int m_tpq;
        /// <summary>
        /// 曲の長さを取得します。(クロック(4分音符は480クロック))
        /// </summary>
        public int TotalClocks = 0;
        protected int m_base_tempo;
        public VsqMaster Master;  // VsqMaster, VsqMixerは通常，最初の非Master Trackに記述されるが，可搬性のため，
        public VsqMixer Mixer;    // ここではVsqFileに直属するものとして取り扱う．
        public Object Tag;

        static readonly byte[] _MTRK = new byte[] { (byte)0x4d, (byte)0x54, (byte)0x72, (byte)0x6b };
        static readonly byte[] _MTHD = new byte[] { (byte)0x4d, (byte)0x54, (byte)0x68, (byte)0x64 };
        static readonly byte[] _MASTER_TRACK = new byte[] { (byte)0x4D, (byte)0x61, (byte)0x73, (byte)0x74, (byte)0x65, (byte)0x72, (byte)0x20, (byte)0x54, (byte)0x72, (byte)0x61, (byte)0x63, (byte)0x6B, };
        static readonly String[] _CURVES = new String[] { "VEL", "DYN", "BRE", "BRI", "CLE", "OPE", "GEN", "POR", "PIT", "PBS" };

#if JAVA
        public VsqFile( UstFile ust ){
            this( "Miku", 1, 4, 4, ust.getBaseTempo() );
#else
        public VsqFile( UstFile ust )
            : this( "Miku", 1, 4, 4, ust.getBaseTempo() ) {
#endif
            int clock_count = 480 * 4; //pre measure = 1、4分の4拍子としたので
            VsqBPList pitch = new VsqBPList( "", 0, -2400, 2400 ); // ノートナンバー×100
            for ( Iterator itr = ust.getTrack( 0 ).getNoteEventIterator(); itr.hasNext(); ) {
                UstEvent ue = (UstEvent)itr.next();
                if ( ue.Lyric != "R" ) {
                    VsqID id = new VsqID( 0 );
                    id.setLength( ue.getLength() );
                    ByRef<String> psymbol = new ByRef<String>( "a" );
                    if ( !SymbolTable.attatch( ue.Lyric, psymbol ) ) {
                        psymbol.value = "a";
                    }
                    id.LyricHandle = new LyricHandle( ue.Lyric, psymbol.value );
                    id.Note = ue.Note;
                    id.type = VsqIDType.Anote;
                    VsqEvent ve = new VsqEvent( clock_count, id );
                    ve.UstEvent = (UstEvent)ue.clone();
                    Track.get( 1 ).addEvent( ve );

                    if ( ue.Pitches != null ) {
                        // PBTypeクロックごとにデータポイントがある
                        int clock = clock_count - ue.PBType;
                        for ( int i = 0; i < ue.Pitches.Length; i++ ) {
                            clock += ue.PBType;
                            pitch.add( clock, (int)ue.Pitches[i] );
                        }
                    }
                }
                if ( ue.Tempo > 0.0f ) {
                    TempoTable.add( new TempoTableEntry( clock_count, (int)(60e6 / ue.Tempo), 0.0 ) );
                }
                clock_count += ue.getLength();
            }
            updateTempoInfo();
            updateTotalClocks();
            updateTimesigInfo();
            reflectPitch( this, 1, pitch );
        }

        /// <summary>
        /// master==MasterPitchControl.Pitchの場合、m_pitchからPITとPBSを再構成。
        /// master==MasterPitchControl.PITandPBSの場合、PITとPBSからm_pitchを再構成
        /// </summary>
        private static void reflectPitch( VsqFile vsq, int track, VsqBPList pitch ) {
            //double offset = AttachedCurves[track - 1].MasterTuningInCent * 100;
            //Vector<Integer> keyclocks = new Vector<Integer>( pitch.getKeys() );
            int keyclock_size = pitch.size();
            VsqBPList pit = new VsqBPList( "pit", 0, -8192, 8191 );
            VsqBPList pbs = new VsqBPList( "pbs", 2, 0, 24 );
            int premeasure_clock = vsq.getPreMeasureClocks();
            int lastpit = pit.getDefault();
            int lastpbs = pbs.getDefault();
            int vpbs = 24;
            int vpit = 0;

            Vector<Integer> parts = new Vector<Integer>();   // 連続した音符ブロックの先頭音符のクロック位置。のリスト
            parts.add( premeasure_clock );
            int lastclock = premeasure_clock;
            for ( Iterator itr = vsq.Track.get( track ).getNoteEventIterator(); itr.hasNext(); ) {
                VsqEvent ve = (VsqEvent)itr.next();
                if ( ve.Clock <= lastclock ) {
                    lastclock = Math.Max( lastclock, ve.Clock + ve.ID.getLength() );
                } else {
                    parts.add( ve.Clock );
                    lastclock = ve.Clock + ve.ID.getLength();
                }
            }

            int parts_size = parts.size();
            for ( int i = 0; i < parts_size; i++ ) {
                int partstart = parts.get( i );
                int partend = int.MaxValue;
                if ( i + 1 < parts.size() ) {
                    partend = parts.get( i + 1 );
                }

                // まず、区間内の最大ピッチベンド幅を調べる
                double max = 0;
                for ( int j = 0; j < keyclock_size; j++ ) {
                    int clock = pitch.getKeyClock( j );
                    if ( clock < partstart ) {
                        continue;
                    }
                    if ( partend <= clock ) {
                        break;
                    }
                    max = Math.Max( max, Math.Abs( pitch.getValue( clock ) / 100.0 ) );
                }

                // 最大ピッチベンド幅を表現できる最小のPBSを計算
                vpbs = (int)(Math.Ceiling( max * 8192.0 / 8191.0 ) + 0.1);
                if ( vpbs <= 0 ) {
                    vpbs = 1;
                }
                double pitch2 = pitch.getValue( partstart ) / 100.0;
                if ( lastpbs != vpbs ) {
                    pbs.add( partstart, vpbs );
                    lastpbs = vpbs;
                }
                vpit = (int)(pitch2 * 8192 / (double)vpbs);
                if ( lastpit != vpit ) {
                    pit.add( partstart, vpit );
                    lastpit = vpit;
                }
                for ( int j = 0; j < keyclock_size; j++ ) {
                    int clock = pitch.getKeyClock( j );
                    if ( clock < partstart ) {
                        continue;
                    }
                    if ( partend <= clock ) {
                        break;
                    }
                    if ( clock != partstart ) {
                        pitch2 = pitch.getElement( j ) / 100.0;
                        vpit = (int)(pitch2 * 8192 / (double)vpbs);
                        if ( lastpit != vpit ) {
                            pit.add( clock, vpit );
                            lastpit = vpit;
                        }
                    }
                }
            }
            vsq.Track.get( track ).setCurve( "pit", pit );
            vsq.Track.get( track ).setCurve( "pbs", pbs );
        }

        /// <summary>
        /// プリセンドタイムの妥当性を判定します
        /// </summary>
        /// <param name="ms_pre_send_time"></param>
        /// <returns></returns>
        public boolean checkPreSendTimeValidity( int ms_pre_send_time ) {
            int track_count = Track.size();
            for ( int i = 1; i < track_count; i++ ) {
                VsqTrack track = Track.get( i );
                for ( Iterator itr = track.getNoteEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    int presend_clock = getPresendClockAt( item.Clock, ms_pre_send_time );
                    if ( item.Clock - presend_clock < 0 ) {
                        return false;
                    }
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// テンポ値を一律order倍します。
        /// </summary>
        /// <param name="order"></param>
        public void speedingUp( double order ) {
            lock ( TempoTable ) {
                int c = TempoTable.size();
                for ( int i = 0; i < c; i++ ) {
                    TempoTable.get( i ).Tempo = (int)(TempoTable.get( i ).Tempo / order);
                }
            }
            updateTempoInfo();
        }

        /// <summary>
        /// このインスタンスに編集を行うコマンドを実行します
        /// </summary>
        /// <param name="command">実行するコマンド</param>
        /// <returns>編集結果を元に戻すためのコマンドを返します</returns>
        public VsqCommand executeCommand( VsqCommand command ) {
#if DEBUG
            PortUtil.println( "VsqFile.Execute(VsqCommand)" );
            PortUtil.println( "    type=" + command.Type );
#endif
            VsqCommandType type = command.Type;
            if ( type == VsqCommandType.CHANGE_PRE_MEASURE ) {
                #region CHANGE_PRE_MEASURE
                VsqCommand ret = VsqCommand.generateCommandChangePreMeasure( Master.PreMeasure );
                int value = (Integer)command.Args[0];
                Master.PreMeasure = value;
                updateTimesigInfo();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.TRACK_ADD ) {
                #region TRACK_ADD
#if DEBUG
                System.Diagnostics.Debug.WriteLine( "    AddTrack" );
#endif
                VsqTrack track = (VsqTrack)command.Args[0];
                VsqMixerEntry mixer = (VsqMixerEntry)command.Args[1];
                int position = (Integer)command.Args[2];
                VsqCommand ret = VsqCommand.generateCommandDeleteTrack( position );
                if ( Track.size() <= 17 ) {
                    Track.insertElementAt( (VsqTrack)track.clone(), position );
                    Mixer.Slave.add( (VsqMixerEntry)mixer.clone() );
                    return ret;
                } else {
                    return null;
                }
                #endregion
            } else if ( type == VsqCommandType.TRACK_DELETE ) {
                #region TRACK_DELETE
                int track = (Integer)command.Args[0];
                VsqCommand ret = VsqCommand.generateCommandAddTrack( Track.get( track ), Mixer.Slave.get( track - 1 ), track );
                Track.removeElementAt( track );
                Mixer.Slave.removeElementAt( track - 1 );
                updateTotalClocks();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.UPDATE_TEMPO ) {
                #region UPDATE_TEMPO
                int clock = (Integer)command.Args[0];
                int tempo = (Integer)command.Args[1];
                int new_clock = (Integer)command.Args[2];

                int index = -1;
                int c = TempoTable.size();
                for ( int i = 0; i < c; i++ ) {
                    if ( TempoTable.get( i ).Clock == clock ) {
                        index = i;
                        break;
                    }
                }
                VsqCommand ret = null;
                if ( index >= 0 ) {
                    if ( tempo <= 0 ) {
                        ret = VsqCommand.generateCommandUpdateTempo( clock, clock, TempoTable.get( index ).Tempo );
                        TempoTable.removeElementAt( index );
                    } else {
                        ret = VsqCommand.generateCommandUpdateTempo( new_clock, clock, TempoTable.get( index ).Tempo );
                        TempoTable.get( index ).Tempo = tempo;
                        TempoTable.get( index ).Clock = new_clock;
                    }
                } else {
                    ret = VsqCommand.generateCommandUpdateTempo( clock, clock, -1 );
                    TempoTable.add( new TempoTableEntry( new_clock, tempo, 0.0 ) );
                }
                updateTempoInfo();
                updateTotalClocks();

                // 編集領域を更新
                int affected_clock = Math.Min( clock, new_clock );
                c = Track.size();
                for ( int i = 1; i < c; i++ ) {
                    if ( affected_clock < Track.get( i ).getEditedStart() ) {
                        Track.get( i ).setEditedStart( affected_clock );
                    }
                    Track.get( i ).setEditedEnd( (int)TotalClocks );
                }
                return ret;
                #endregion
            } else if ( type == VsqCommandType.UPDATE_TEMPO_RANGE ) {
                #region UPDATE_TEMPO_RANGE
                int[] clocks = (int[])command.Args[0];
                int[] tempos = (int[])command.Args[1];
                int[] new_clocks = (int[])command.Args[2];
                int[] new_tempos = new int[tempos.Length];
                int affected_clock = int.MaxValue;
                for ( int i = 0; i < clocks.Length; i++ ) {
                    int index = -1;
                    affected_clock = Math.Min( affected_clock, clocks[i] );
                    affected_clock = Math.Min( affected_clock, new_clocks[i] );
                    int tempo_table_count = TempoTable.size();
                    for ( int j = 0; j < tempo_table_count; j++ ) {
                        if ( TempoTable.get( j ).Clock == clocks[i] ) {
                            index = j;
                            break;
                        }
                    }
                    if ( index >= 0 ) {
                        new_tempos[i] = TempoTable.get( index ).Tempo;
                        if ( tempos[i] <= 0 ) {
                            TempoTable.removeElementAt( index );
                        } else {
                            TempoTable.get( index ).Tempo = tempos[i];
                            TempoTable.get( index ).Clock = new_clocks[i];
                        }
                    } else {
                        new_tempos[i] = -1;
                        TempoTable.add( new TempoTableEntry( new_clocks[i], tempos[i], 0.0 ) );
                    }
                }
                updateTempoInfo();
                updateTotalClocks();
                int track_count = Track.size();
                for ( int i = 1; i < track_count; i++ ) {
                    if ( affected_clock < Track.get( i ).getEditedStart() ) {
                        Track.get( i ).setEditedStart( affected_clock );
                    }
                    Track.get( i ).setEditedEnd( (int)TotalClocks );
                }
                return VsqCommand.generateCommandUpdateTempoRange( new_clocks, clocks, new_tempos );
                #endregion
            } else if ( type == VsqCommandType.UPDATE_TIMESIG ) {
                #region UPDATE_TIMESIG
                int barcount = (Integer)command.Args[0];
                int numerator = (Integer)command.Args[1];
                int denominator = (Integer)command.Args[2];
                int new_barcount = (Integer)command.Args[3];
                int index = -1;
                int timesig_table_count = TimesigTable.size();
                for ( int i = 0; i < timesig_table_count; i++ ) {
                    if ( barcount == TimesigTable.get( i ).BarCount ) {
                        index = i;
                        break;
                    }
                }
                VsqCommand ret = null;
                if ( index >= 0 ) {
                    if ( numerator <= 0 ) {
                        ret = VsqCommand.generateCommandUpdateTimesig( barcount, barcount, TimesigTable.get( index ).Numerator, TimesigTable.get( index ).Denominator );
                        TimesigTable.removeElementAt( index );
                    } else {
                        ret = VsqCommand.generateCommandUpdateTimesig( new_barcount, barcount, TimesigTable.get( index ).Numerator, TimesigTable.get( index ).Denominator );
                        TimesigTable.get( index ).BarCount = new_barcount;
                        TimesigTable.get( index ).Numerator = numerator;
                        TimesigTable.get( index ).Denominator = denominator;
                    }
                } else {
                    ret = VsqCommand.generateCommandUpdateTimesig( new_barcount, new_barcount, -1, -1 );
                    TimesigTable.add( new TimeSigTableEntry( 0, numerator, denominator, new_barcount ) );
                }
                updateTimesigInfo();
                updateTotalClocks();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.UPDATE_TIMESIG_RANGE ) {
                #region UPDATE_TIMESIG_RANGE
                int[] barcounts = (int[])command.Args[0];
                int[] numerators = (int[])command.Args[1];
                int[] denominators = (int[])command.Args[2];
                int[] new_barcounts = (int[])command.Args[3];
                int[] new_numerators = new int[numerators.Length];
                int[] new_denominators = new int[denominators.Length];
                for ( int i = 0; i < barcounts.Length; i++ ) {
                    int index = -1;
                    // すでに拍子が登録されているかどうかを検査
                    int timesig_table_count = TimesigTable.size();
                    for ( int j = 0; j < timesig_table_count; j++ ) {
                        if ( TimesigTable.get( j ).BarCount == barcounts[i] ) {
                            index = j;
                            break;
                        }
                    }
                    if ( index >= 0 ) {
                        // 登録されている場合
                        new_numerators[i] = TimesigTable.get( index ).Numerator;
                        new_denominators[i] = TimesigTable.get( index ).Denominator;
                        if ( numerators[i] <= 0 ) {
                            TimesigTable.removeElementAt( index );
                        } else {
                            TimesigTable.get( index ).BarCount = new_barcounts[i];
                            TimesigTable.get( index ).Numerator = numerators[i];
                            TimesigTable.get( index ).Denominator = denominators[i];
                        }
                    } else {
                        // 登録されていない場合
                        new_numerators[i] = -1;
                        new_denominators[i] = -1;
                        TimesigTable.add( new TimeSigTableEntry( 0, numerators[i], denominators[i], new_barcounts[i] ) );
                    }
                }
                updateTimesigInfo();
                updateTotalClocks();
                return VsqCommand.generateCommandUpdateTimesigRange( new_barcounts, barcounts, new_numerators, new_denominators );
                #endregion
            } else if ( type == VsqCommandType.REPLACE ) {
                #region REPLACE
                VsqFile vsq = (VsqFile)command.Args[0];
                VsqFile inv = (VsqFile)this.clone();
                Track.clear();
                int track_count = vsq.Track.size();
                for ( int i = 0; i < track_count; i++ ) {
                    Track.add( (VsqTrack)vsq.Track.get( i ).clone() );
                }

                TempoTable.clear();
                int tempo_table_count = vsq.TempoTable.size();
                for ( int i = 0; i < tempo_table_count; i++ ) {
                    TempoTable.add( (TempoTableEntry)vsq.TempoTable.get( i ).clone() );
                }

                TimesigTable.clear();
                int timesig_table_count = vsq.TimesigTable.size();
                for ( int i = 0; i < timesig_table_count; i++ ) {
                    TimesigTable.add( (TimeSigTableEntry)vsq.TimesigTable.get( i ).clone() );
                }
                m_tpq = vsq.m_tpq;
                TotalClocks = vsq.TotalClocks;
                m_base_tempo = vsq.m_base_tempo;
                Master = (VsqMaster)vsq.Master.clone();
                Mixer = (VsqMixer)vsq.Mixer.clone();
                updateTotalClocks();
                return VsqCommand.generateCommandReplace( inv );
                #endregion
            } else if ( type == VsqCommandType.EVENT_ADD ) {
                #region EVENT_ADD
                int track = (Integer)command.Args[0];
                VsqEvent item = (VsqEvent)command.Args[1];
                Track.get( track ).addEvent( item );
                VsqCommand ret = VsqCommand.generateCommandEventDelete( track, item.InternalID );
                updateTotalClocks();
                if ( item.Clock < Track.get( track ).getEditedStart() ) {
                    Track.get( track ).setEditedStart( item.Clock );
                }
                if ( Track.get( track ).getEditedEnd() < item.Clock + item.ID.getLength() ) {
                    Track.get( track ).setEditedEnd( item.Clock + item.ID.getLength() );
                }
                Track.get( track ).sortEvent();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.EVENT_ADD_RANGE ) {
                #region EVENT_ADD_RANGE
#if DEBUG
                PortUtil.println( "    TrackAddNoteRange" );
#endif
                int track = (Integer)command.Args[0];
                VsqEvent[] items = (VsqEvent[])command.Args[1];
                Vector<Integer> inv_ids = new Vector<Integer>();
                int min_clock = (int)TotalClocks;
                int max_clock = 0;
                VsqTrack target = Track.get( track );
                for ( int i = 0; i < items.Length; i++ ) {
                    VsqEvent item = (VsqEvent)items[i].clone();
                    min_clock = Math.Min( min_clock, item.Clock );
                    max_clock = Math.Max( max_clock, item.Clock + item.ID.getLength() );
#if DEBUG
                    Console.Write( "        i=" + i + "; item.InternalID=" + item.InternalID );
#endif
                    target.addEvent( item );
                    inv_ids.add( item.InternalID );
#if DEBUG
                    PortUtil.println( " => " + item.InternalID );
#endif
                }
                updateTotalClocks();
                if ( min_clock < target.getEditedStart() ) {
                    target.setEditedStart( min_clock );
                }
                if ( target.getEditedEnd() < max_clock ) {
                    target.setEditedEnd( max_clock );
                }
                target.sortEvent();
                return VsqCommand.generateCommandEventDeleteRange( track, inv_ids );
                #endregion
            } else if ( type == VsqCommandType.EVENT_DELETE ) {
                #region EVENT_DELETE
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                VsqEvent[] original = new VsqEvent[1];
                VsqTrack target = Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        original[0] = (VsqEvent)item.clone();
                        break;
                    }
                }
                if ( original[0].Clock < target.getEditedStart() ) {
                    target.setEditedStart( original[0].Clock );
                }
                if ( target.getEditedEnd() < original[0].Clock + original[0].ID.getLength() ) {
                    target.setEditedEnd( original[0].Clock + original[0].ID.getLength() );
                }
                VsqCommand ret = VsqCommand.generateCommandEventAddRange( track, original );
                int count = target.getEventCount();
                for ( int i = 0; i < count; i++ ) {
                    if ( target.getEvent( i ).InternalID == internal_id ) {
                        target.removeEvent( i );
                        break;
                    }
                }
                updateTotalClocks();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.EVENT_DELETE_RANGE ) {
                #region EVENT_DELETE_RANGE
                Vector<Integer> internal_ids = (Vector<Integer>)command.Args[1];
                int track = (Integer)command.Args[0];
                Vector<VsqEvent> inv = new Vector<VsqEvent>();
                int min_clock = int.MaxValue;
                int max_clock = int.MinValue;
                VsqTrack target = this.Track.get( track );
                int count = internal_ids.size();
                for ( int j = 0; j < count; j++ ) {
                    for ( int i = 0; i < target.getEventCount(); i++ ) {
                        VsqEvent item = target.getEvent( i );
                        if ( internal_ids.get( j ) == item.InternalID ) {
                            inv.add( (VsqEvent)item.clone() );
                            min_clock = Math.Min( min_clock, item.Clock );
                            max_clock = Math.Max( max_clock, item.Clock + item.ID.getLength() );
                            target.removeEvent( i );
                            break;
                        }
                    }
                }
                updateTotalClocks();
                target.setEditedStart( min_clock );
                target.setEditedEnd( max_clock );
                return VsqCommand.generateCommandEventAddRange( track, inv.toArray( new VsqEvent[] { } ) );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_CLOCK ) {
                #region EVENT_CHANGE_CLOCK
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int value = (Integer)command.Args[2];
                VsqTrack target = this.Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeClock( track, internal_id, item.Clock );
                        int min = Math.Min( item.Clock, value );
                        int max = Math.Max( item.Clock + item.ID.getLength(), value + item.ID.getLength() );
                        target.setEditedStart( min );
                        target.setEditedEnd( max );
                        item.Clock = value;
                        updateTotalClocks();
                        target.sortEvent();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_LYRIC ) {
                #region EVENT_CHANGE_LYRIC
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                String phrase = (String)command.Args[2];
                String phonetic_symbol = (String)command.Args[3];
                boolean protect_symbol = (Boolean)command.Args[4];
                VsqTrack target = this.Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        if ( item.ID.type == VsqIDType.Anote ) {
                            VsqCommand ret = VsqCommand.generateCommandEventChangeLyric( track, internal_id, item.ID.LyricHandle.L0.Phrase, item.ID.LyricHandle.L0.getPhoneticSymbol(), item.ID.LyricHandle.L0.PhoneticSymbolProtected );
                            item.ID.LyricHandle.L0.Phrase = phrase;
                            item.ID.LyricHandle.L0.setPhoneticSymbol( phonetic_symbol );
                            item.ID.LyricHandle.L0.PhoneticSymbolProtected = protect_symbol;
                            target.setEditedStart( item.Clock );
                            target.setEditedEnd( item.Clock + item.ID.getLength() );
                            updateTotalClocks();
                            return ret;
                        }
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_NOTE ) {
                #region EVENT_CHANGE_NOTE
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int note = (Integer)command.Args[2];
                VsqTrack target = this.Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeNote( track, internal_id, item.ID.Note );
                        item.ID.Note = note;
                        updateTotalClocks();
                        target.setEditedStart( item.Clock );
                        target.setEditedEnd( item.Clock + item.ID.getLength() );
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_CLOCK_AND_NOTE ) {
                #region EVENT_CHANGE_CLOCK_AND_NOTE
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int clock = (Integer)command.Args[2];
                int note = (Integer)command.Args[3];
                VsqTrack target = this.Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeClockAndNote( track, internal_id, item.Clock, item.ID.Note );
                        int min = Math.Min( item.Clock, clock );
                        int max = Math.Max( item.Clock + item.ID.getLength(), clock + item.ID.getLength() );
                        target.setEditedStart( min );
                        target.setEditedEnd( max );
                        item.Clock = clock;
                        item.ID.Note = note;
                        target.sortEvent();
                        updateTotalClocks();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_EDIT ) {
                #region TRACK_CURVE_EDIT
                int track = (Integer)command.Args[0];
                String curve = (String)command.Args[1];
                Vector<BPPair> com = (Vector<BPPair>)command.Args[2];
                VsqCommand inv = null;
                Vector<BPPair> edit = new Vector<BPPair>();
                VsqBPList target_list = Track.get( track ).getCurve( curve );
                if ( com != null ) {
                    if ( com.size() > 0 ) {
                        int start_clock = com.get( 0 ).Clock;
                        int end_clock = com.get( 0 ).Clock;
                        for ( Iterator itr = com.iterator(); itr.hasNext(); ) {
                            BPPair item = (BPPair)itr.next();
                            start_clock = Math.Min( start_clock, item.Clock );
                            end_clock = Math.Max( end_clock, item.Clock );
                        }
                        Track.get( track ).setEditedStart( start_clock );
                        Track.get( track ).setEditedEnd( end_clock );
                        int start_value = target_list.getValue( start_clock );
                        int end_value = target_list.getValue( end_clock );
                        for ( Iterator i = target_list.keyClockIterator(); i.hasNext(); ) {
                            int clock = (Integer)i.next();
                            if ( start_clock <= clock && clock <= end_clock ) {
                                edit.add( new BPPair( clock, target_list.getValue( clock ) ) );
                            }
                        }
                        boolean start_found = false;
                        boolean end_found = false;
                        int count = edit.size();
                        for ( int i = 0; i < count; i++ ) {
                            if ( edit.get( i ).Clock == start_clock ) {
                                start_found = true;
                                edit.get( i ).Value = start_value;
                                if ( start_found && end_found ) {
                                    break;
                                }
                            }
                            if ( edit.get( i ).Clock == end_clock ) {
                                end_found = true;
                                edit.get( i ).Value = end_value;
                                if ( start_found && end_found ) {
                                    break;
                                }
                            }
                        }
                        if ( !start_found ) {
                            edit.add( new BPPair( start_clock, start_value ) );
                        }
                        if ( !end_found ) {
                            edit.add( new BPPair( end_clock, end_value ) );
                        }

                        // 並べ替え
                        Collections.sort( edit );
                        inv = VsqCommand.generateCommandTrackCurveEdit( track, curve, edit );
                    } else if ( com.size() == 0 ) {
                        inv = VsqCommand.generateCommandTrackCurveEdit( track, curve, new Vector<BPPair>() );
                    }
                }

                updateTotalClocks();
                if ( com.size() == 0 ) {
                    return inv;
                } else if ( com.size() == 1 ) {
                    boolean found = false;
                    for ( Iterator itr = target_list.keyClockIterator(); itr.hasNext(); ) {
                        int clock = (Integer)itr.next();
                        if ( clock == com.get( 0 ).Clock ) {
                            found = true;
                            target_list.add( clock, com.get( 0 ).Value );
                            break;
                        }
                    }
                    if ( !found ) {
                        target_list.add( com.get( 0 ).Clock, com.get( 0 ).Value );
                    }
                } else {
                    int start_clock = com.get( 0 ).Clock;
                    int end_clock = com.get( com.size() - 1 ).Clock;
                    boolean removed = true;
                    while ( removed ) {
                        removed = false;
                        for ( Iterator itr = target_list.keyClockIterator(); itr.hasNext(); ) {
                            int clock = (Integer)itr.next();
                            if ( start_clock <= clock && clock <= end_clock ) {
                                target_list.remove( clock );
                                removed = true;
                                break;
                            }
                        }
                    }
                    for ( Iterator itr = com.iterator(); itr.hasNext(); ) {
                        BPPair item = (BPPair)itr.next();
                        target_list.add( item.Clock, item.Value );
                    }
                }
                return inv;
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_EDIT2 ) {
                #region TRACK_CURVE_EDIT2
                int track = (Integer)command.Args[0];
                String curve = (String)command.Args[1];
                Vector<Long> delete = (Vector<Long>)command.Args[2];
                TreeMap<Integer, VsqBPPair> add = (TreeMap<Integer, VsqBPPair>)command.Args[3];

                Vector<Long> inv_delete = new Vector<Long>();
                TreeMap<Integer, VsqBPPair> inv_add = new TreeMap<Integer, VsqBPPair>();
                processTrackCurveEdit( track, curve, delete, add, inv_delete, inv_add );
                updateTotalClocks();

                return VsqCommand.generateCommandTrackCurveEdit2( track, curve, inv_delete, inv_add );
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_EDIT2_ALL ) {
                #region TRACK_CURVE_EDIT2_ALL
                int track = (Integer)command.Args[0];
                Vector<String> curve = (Vector<String>)command.Args[1];
                Vector<Vector<Long>> delete = (Vector<Vector<Long>>)command.Args[2];
                Vector<TreeMap<Integer, VsqBPPair>> add = (Vector<TreeMap<Integer, VsqBPPair>>)command.Args[3];

                int c = curve.size();
                Vector<Vector<Long>> inv_delete = new Vector<Vector<Long>>();
                Vector<TreeMap<Integer, VsqBPPair>> inv_add = new Vector<TreeMap<Integer, VsqBPPair>>();
                for ( int i = 0; i < c; i++ ) {
                    Vector<Long> part_inv_delete = new Vector<Long>();
                    TreeMap<Integer, VsqBPPair> part_inv_add = new TreeMap<Integer, VsqBPPair>();
                    processTrackCurveEdit( track, curve.get( i ), delete.get( i ), add.get( i ), part_inv_delete, part_inv_add );
                    inv_delete.add( part_inv_delete );
                    inv_add.add( part_inv_add );
                }
                updateTotalClocks();

                return VsqCommand.generateCommandTrackCurveEdit2All( track, curve, inv_delete, inv_add );
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_REPLACE ) {
                #region TRACK_CURVE_REPLACE
                int track = (Integer)command.Args[0];
                String target_curve = (String)command.Args[1];
                VsqBPList bplist = (VsqBPList)command.Args[2];
                VsqCommand inv = VsqCommand.generateCommandTrackCurveReplace( track, target_curve, Track.get( track ).getCurve( target_curve ) );
                Track.get( track ).setCurve( target_curve, bplist );
                return inv;
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_REPLACE_RANGE ) {
                #region TRACK_CURVE_REPLACE_RANGE
                int track = (Integer)command.Args[0];
                String[] target_curve = (String[])command.Args[1];
                VsqBPList[] bplist = (VsqBPList[])command.Args[2];
                VsqBPList[] inv_bplist = new VsqBPList[bplist.Length];
                VsqTrack work = Track.get( track );
                for ( int i = 0; i < target_curve.Length; i++ ) {
                    inv_bplist[i] = work.getCurve( target_curve[i] );
                }
                VsqCommand inv = VsqCommand.generateCommandTrackCurveReplaceRange( track, target_curve, inv_bplist );
                for ( int i = 0; i < target_curve.Length; i++ ) {
                    work.setCurve( target_curve[i], bplist[i] );
                }
                return inv;
                #endregion
            } else if ( type == VsqCommandType.TRACK_CURVE_EDIT_RANGE ) {
                #region TRACK_CURVE_EDIT_RANGE
                int track = (Integer)command.Args[0];
                Vector<String> curves = (Vector<String>)command.Args[1];
                Vector<Vector<BPPair>> coms = (Vector<Vector<BPPair>>)command.Args[2];
                Vector<Vector<BPPair>> inv_coms = new Vector<Vector<BPPair>>();
                VsqCommand inv = null;

                int count = curves.size();
                for ( int k = 0; k < count; k++ ) {
                    String curve = curves.get( k );
                    Vector<BPPair> com = coms.get( k );
                    //SortedList<int, int> list = Tracks[track][curve].List;
                    Vector<BPPair> edit = new Vector<BPPair>();
                    if ( com != null ) {
                        if ( com.size() > 0 ) {
                            int start_clock = com.get( 0 ).Clock;
                            int end_clock = com.get( 0 ).Clock;
                            for ( Iterator itr = com.iterator(); itr.hasNext(); ) {
                                BPPair item = (BPPair)itr.next();
                                start_clock = Math.Min( start_clock, item.Clock );
                                end_clock = Math.Max( end_clock, item.Clock );
                            }
                            Track.get( track ).setEditedStart( start_clock );
                            Track.get( track ).setEditedEnd( end_clock );
                            int start_value = Track.get( track ).getCurve( curve ).getValue( start_clock );
                            int end_value = Track.get( track ).getCurve( curve ).getValue( end_clock );
                            for ( Iterator itr = Track.get( track ).getCurve( curve ).keyClockIterator(); itr.hasNext(); ) {
                                int clock = (Integer)itr.next();
                                if ( start_clock <= clock && clock <= end_clock ) {
                                    edit.add( new BPPair( clock, Track.get( track ).getCurve( curve ).getValue( clock ) ) );
                                }
                            }
                            boolean start_found = false;
                            boolean end_found = false;
                            for ( int i = 0; i < edit.size(); i++ ) {
                                if ( edit.get( i ).Clock == start_clock ) {
                                    start_found = true;
                                    edit.get( i ).Value = start_value;
                                    if ( start_found && end_found ) {
                                        break;
                                    }
                                }
                                if ( edit.get( i ).Clock == end_clock ) {
                                    end_found = true;
                                    edit.get( i ).Value = end_value;
                                    if ( start_found && end_found ) {
                                        break;
                                    }
                                }
                            }
                            if ( !start_found ) {
                                edit.add( new BPPair( start_clock, start_value ) );
                            }
                            if ( !end_found ) {
                                edit.add( new BPPair( end_clock, end_value ) );
                            }

                            // 並べ替え
                            Collections.sort( edit );
                            inv_coms.add( edit );
                            //inv = generateCommandTrackEditCurve( track, curve, edit );
                        } else if ( com.size() == 0 ) {
                            //inv = generateCommandTrackEditCurve( track, curve, new Vector<BPPair>() );
                            inv_coms.add( new Vector<BPPair>() );
                        }
                    }

                    updateTotalClocks();
                    if ( com.size() == 0 ) {
                        return inv;
                    } else if ( com.size() == 1 ) {
                        boolean found = false;
                        for ( Iterator itr = Track.get( track ).getCurve( curve ).keyClockIterator(); itr.hasNext(); ) {
                            int clock = (Integer)itr.next();
                            if ( clock == com.get( 0 ).Clock ) {
                                found = true;
                                Track.get( track ).getCurve( curve ).add( clock, com.get( 0 ).Value );
                                break;
                            }
                        }
                        if ( !found ) {
                            Track.get( track ).getCurve( curve ).add( com.get( 0 ).Clock, com.get( 0 ).Value );
                        }
                    } else {
                        int start_clock = com.get( 0 ).Clock;
                        int end_clock = com.get( com.size() - 1 ).Clock;
                        boolean removed = true;
                        while ( removed ) {
                            removed = false;
                            for ( Iterator itr = Track.get( track ).getCurve( curve ).keyClockIterator(); itr.hasNext(); ) {
                                int clock = (Integer)itr.next();
                                if ( start_clock <= clock && clock <= end_clock ) {
                                    Track.get( track ).getCurve( curve ).remove( clock );
                                    removed = true;
                                    break;
                                }
                            }
                        }
                        for ( Iterator itr = com.iterator(); itr.hasNext(); ) {
                            BPPair item = (BPPair)itr.next();
                            Track.get( track ).getCurve( curve ).add( item.Clock, item.Value );
                        }
                    }
                }
                return VsqCommand.generateCommandTrackCurveEditRange( track, curves, inv_coms );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_VELOCITY ) {
                #region EVENT_CHANGE_VELOCITY
                int track = (Integer)command.Args[0];
                Vector<ValuePair<Integer, Integer>> veloc = (Vector<ValuePair<Integer, Integer>>)command.Args[1];
                Vector<ValuePair<Integer, Integer>> inv = new Vector<ValuePair<Integer, Integer>>();
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent ev = (VsqEvent)itr.next();
                    for ( Iterator itr2 = veloc.iterator(); itr2.hasNext(); ) {
                        ValuePair<Integer, Integer> add = (ValuePair<Integer, Integer>)itr2.next();
                        if ( ev.InternalID == add.getKey() ) {
                            inv.add( new ValuePair<Integer, Integer>( ev.InternalID, ev.ID.Dynamics ) );
                            ev.ID.Dynamics = add.getValue();
                            Track.get( track ).setEditedStart( ev.Clock );
                            Track.get( track ).setEditedEnd( ev.Clock + ev.ID.getLength() );
                            break;
                        }
                    }
                }
                return VsqCommand.generateCommandEventChangeVelocity( track, inv );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_ACCENT ) {
                #region EVENT_CHANGE_ACCENT
                int track = (Integer)command.Args[0];
                Vector<ValuePair<Integer, Integer>> veloc = (Vector<ValuePair<Integer, Integer>>)command.Args[1];
                Vector<ValuePair<Integer, Integer>> inv = new Vector<ValuePair<Integer, Integer>>();
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent ev = (VsqEvent)itr.next();
                    for ( Iterator itr2 = veloc.iterator(); itr2.hasNext(); ) {
                        ValuePair<Integer, Integer> add = (ValuePair<Integer, Integer>)itr2.next();
                        if ( ev.InternalID == add.getKey() ) {
                            inv.add( new ValuePair<Integer, Integer>( ev.InternalID, ev.ID.DEMaccent ) );
                            ev.ID.DEMaccent = add.getValue();
                            Track.get( track ).setEditedStart( ev.Clock );
                            Track.get( track ).setEditedEnd( ev.Clock + ev.ID.getLength() );
                            break;
                        }
                    }
                }
                return VsqCommand.generateCommandEventChangeAccent( track, inv );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_DECAY ) {
                #region EVENT_CHANGE_DECAY
                int track = (Integer)command.Args[0];
                Vector<ValuePair<Integer, Integer>> veloc = (Vector<ValuePair<Integer, Integer>>)command.Args[1];
                Vector<ValuePair<Integer, Integer>> inv = new Vector<ValuePair<Integer, Integer>>();
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent ev = (VsqEvent)itr.next();
                    for ( Iterator itr2 = veloc.iterator(); itr2.hasNext(); ) {
                        ValuePair<Integer, Integer> add = (ValuePair<Integer, Integer>)itr2.next();
                        if ( ev.InternalID == add.getKey() ) {
                            inv.add( new ValuePair<Integer, Integer>( ev.InternalID, ev.ID.DEMdecGainRate ) );
                            ev.ID.DEMdecGainRate = add.getValue();
                            Track.get( track ).setEditedStart( ev.Clock );
                            Track.get( track ).setEditedEnd( ev.Clock + ev.ID.getLength() );
                            break;
                        }
                    }
                }
                return VsqCommand.generateCommandEventChangeDecay( track, inv );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_LENGTH ) {
                #region EVENT_CHANGE_LENGTH
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int new_length = (Integer)command.Args[2];
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeLength( track, internal_id, item.ID.getLength() );
                        Track.get( track ).setEditedStart( item.Clock );
                        int max = Math.Max( item.Clock + item.ID.getLength(), item.Clock + new_length );
                        Track.get( track ).setEditedEnd( max );
                        item.ID.setLength( new_length );
                        updateTotalClocks();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_CLOCK_AND_LENGTH ) {
                #region EVENT_CHANGE_CLOCK_AND_LENGTH
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int new_clock = (Integer)command.Args[2];
                int new_length = (Integer)command.Args[3];
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeClockAndLength( track, internal_id, item.Clock, item.ID.getLength() );
                        int min = Math.Min( item.Clock, new_clock );
                        int max_length = Math.Max( item.ID.getLength(), new_length );
                        int max = Math.Max( item.Clock + max_length, new_clock + max_length );
                        Track.get( track ).setEditedStart( min );
                        Track.get( track ).setEditedEnd( max );
                        item.ID.setLength( new_length );
                        item.Clock = new_clock;
                        Track.get( track ).sortEvent();
                        updateTotalClocks();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_ID_CONTAINTS ) {
                #region EVENT_CHANGE_ID_CONTAINTS
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                VsqID value = (VsqID)command.Args[2];
                for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeIDContaints( track, internal_id, item.ID );
                        int max_length = Math.Max( item.ID.getLength(), value.getLength() );
                        Track.get( track ).setEditedStart( item.Clock );
                        Track.get( track ).setEditedEnd( item.Clock + max_length );
                        item.ID = (VsqID)value.clone();
                        if ( item.ID.type == VsqIDType.Singer ) {
#if DEBUG
                            PortUtil.println( "    EventChangeIDContaints" );
#endif
                            // 歌手変更の場合、次に現れる歌手変更の位置まで編集の影響が及ぶ
                            boolean found = false;
                            for ( Iterator itr2 = Track.get( track ).getSingerEventIterator(); itr2.hasNext(); ) {
                                VsqEvent item2 = (VsqEvent)itr2.next();
                                if ( item.Clock < item2.Clock ) {
                                    Track.get( track ).setEditedEnd( item2.Clock );
                                    found = true;
                                    break;
                                }
                            }
                            if ( !found ) {
                                // 変更対象が、該当トラック最後の歌手変更イベントだった場合
                                if ( Track.get( track ).getEventCount() >= 1 ) {
                                    VsqEvent last_event = Track.get( track ).getEvent( Track.get( track ).getEventCount() - 1 );
                                    Track.get( track ).setEditedEnd( last_event.Clock + last_event.ID.getLength() );
                                }
                            }
#if DEBUG
                            PortUtil.println( "        EditedStart,EditedEnd=" + Track.get( track ).getEditedStart() + "," + Track.get( track ).getEditedEnd() );
#endif
                        }
                        updateTotalClocks();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_ID_CONTAINTS_RANGE ) {
                #region EVENT_CHANGE_ID_CONTAINTS_RANGE
                int track = (Integer)command.Args[0];
                int[] internal_ids = (int[])command.Args[1];
                VsqID[] values = (VsqID[])command.Args[2];
                VsqID[] inv_values = new VsqID[values.Length];
                for ( int i = 0; i < internal_ids.Length; i++ ) {
                    for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                        VsqEvent item = (VsqEvent)itr.next();
                        if ( item.InternalID == internal_ids[i] ) {
                            inv_values[i] = (VsqID)item.ID.clone();
                            int max_length = Math.Max( item.ID.getLength(), values[i].getLength() );
                            Track.get( track ).setEditedStart( item.Clock );
                            Track.get( track ).setEditedEnd( item.Clock + max_length );
                            item.ID = (VsqID)values[i].clone();
                            if ( item.ID.type == VsqIDType.Singer ) {
                                // 歌手変更の場合、次に現れる歌手変更の位置まで編集の影響が及ぶ
                                boolean found = false;
                                for ( Iterator itr2 = Track.get( track ).getSingerEventIterator(); itr2.hasNext(); ) {
                                    VsqEvent item2 = (VsqEvent)itr2.next();
                                    if ( item.Clock < item2.Clock ) {
                                        Track.get( track ).setEditedEnd( item2.Clock );
                                        found = true;
                                        break;
                                    }
                                }
                                if ( !found ) {
                                    // 変更対象が、該当トラック最後の歌手変更イベントだった場合
                                    if ( Track.get( track ).getEventCount() >= 1 ) {
                                        VsqEvent last_event = Track.get( track ).getEvent( Track.get( track ).getEventCount() - 1 );
                                        Track.get( track ).setEditedEnd( last_event.Clock + last_event.ID.getLength() );
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
                updateTotalClocks();
                return VsqCommand.generateCommandEventChangeIDContaintsRange( track, internal_ids, inv_values );
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_CLOCK_AND_ID_CONTAINTS ) {
                #region EVENT_CHANGE_CLOCK_AND_ID_CONTAINTS
                int track = (Integer)command.Args[0];
                int internal_id = (Integer)command.Args[1];
                int new_clock = (Integer)command.Args[2];
                VsqID value = (VsqID)command.Args[3];
                VsqTrack target = Track.get( track );
                for ( Iterator itr = target.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent item = (VsqEvent)itr.next();
                    if ( item.InternalID == internal_id ) {
                        VsqCommand ret = VsqCommand.generateCommandEventChangeClockAndIDContaints( track, internal_id, item.Clock, item.ID );
                        int max_length = Math.Max( item.ID.getLength(), value.getLength() );
                        int min = Math.Min( item.Clock, new_clock );
                        int max = Math.Max( item.Clock + max_length, new_clock + max_length );
                        item.ID = (VsqID)value.clone();
                        item.Clock = new_clock;
                        target.setEditedStart( min );
                        target.setEditedEnd( max );
                        target.sortEvent();
                        updateTotalClocks();
                        return ret;
                    }
                }
                return null;
                #endregion
            } else if ( type == VsqCommandType.EVENT_CHANGE_CLOCK_AND_ID_CONTAINTS_RANGE ) {
                #region EVENT_CHANGE_CLOCK_AND_ID_CONTAINTS_RANGE
                int track = (Integer)command.Args[0];
                int[] internal_ids = (int[])command.Args[1];
                int[] clocks = (int[])command.Args[2];
                VsqID[] values = (VsqID[])command.Args[3];
                Vector<VsqID> inv_id = new Vector<VsqID>();
                Vector<Integer> inv_clock = new Vector<Integer>();
                for ( int i = 0; i < internal_ids.Length; i++ ) {
                    for ( Iterator itr = Track.get( track ).getEventIterator(); itr.hasNext(); ) {
                        VsqEvent item = (VsqEvent)itr.next();
                        if ( item.InternalID == internal_ids[i] ) {
                            inv_id.add( (VsqID)item.ID.clone() );
                            inv_clock.add( item.Clock );
                            int max_length = Math.Max( item.ID.getLength(), values[i].getLength() );
                            int min = Math.Min( item.Clock, clocks[i] );
                            int max = Math.Max( item.Clock + max_length, clocks[i] + max_length );
                            Track.get( track ).setEditedStart( min );
                            Track.get( track ).setEditedEnd( max );
                            item.ID = (VsqID)values[i].clone();
                            item.Clock = clocks[i];
                            break;
                        }
                    }
                }
                Track.get( track ).sortEvent();
                updateTotalClocks();
                return VsqCommand.generateCommandEventChangeClockAndIDContaintsRange(
                    track,
                    internal_ids,
                    PortUtil.convertIntArray( inv_clock.toArray( new Integer[] { } ) ),
                    inv_id.toArray( new VsqID[] { } ) );
#if DEBUG
                PortUtil.println( "    TrackChangeClockAndIDContaintsRange" );
                PortUtil.println( "    track=" + track );
                for ( int i = 0; i < internal_ids.Length; i++ ) {
                    PortUtil.println( "    id=" + internal_ids[i] + "; clock=" + clocks[i] + "; ID=" + values[i].ToString() );
                }
#endif
                #endregion
            } else if ( type == VsqCommandType.TRACK_CHANGE_NAME ) {
                #region TRACK_CHANGE_NAME
                int track = (Integer)command.Args[0];
                String new_name = (String)command.Args[1];
                VsqCommand ret = VsqCommand.generateCommandTrackChangeName( track, Track.get( track ).getName() );
                Track.get( track ).setName( new_name );
                return ret;
                #endregion
            } else if ( type == VsqCommandType.TRACK_REPLACE ) {
                #region TRACK_REPLACE
                int track = (Integer)command.Args[0];
                VsqTrack item = (VsqTrack)command.Args[1];
                VsqCommand ret = VsqCommand.generateCommandTrackReplace( track, Track.get( track ) );
                Track.set( track, item );
                updateTotalClocks();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.TRACK_CHANGE_PLAY_MODE ) {
                #region TRACK_CHANGE_PLAY_MODE
                int track = (Integer)command.Args[0];
                int play_mode = (Integer)command.Args[1];
                VsqCommand ret = VsqCommand.generateCommandTrackChangePlayMode( track, Track.get( track ).getCommon().PlayMode );
                Track.get( track ).getCommon().PlayMode = play_mode;
                return ret;
                #endregion
            } else if ( type == VsqCommandType.EVENT_REPLACE ) {
                #region EVENT_REPLACE
                int track = (Integer)command.Args[0];
                VsqEvent item = (VsqEvent)command.Args[1];
                VsqCommand ret = null;
                for ( int i = 0; i < Track.get( track ).getEventCount(); i++ ) {
                    VsqEvent ve = Track.get( track ).getEvent( i );
                    if ( ve.InternalID == item.InternalID ) {
                        ret = VsqCommand.generateCommandEventReplace( track, ve );
                        Track.get( track ).setEvent( i, item );
                        break;
                    }
                }
                Track.get( track ).sortEvent();
                updateTotalClocks();
                return ret;
                #endregion
            } else if ( type == VsqCommandType.EVENT_REPLACE_RANGE ) {
                #region EVENT_REPLACE_RANGE
                int track = (Integer)command.Args[0];
                VsqEvent[] items = (VsqEvent[])command.Args[1];
                VsqCommand ret = null;
                VsqEvent[] reverse = new VsqEvent[items.Length];
                for ( int i = 0; i < items.Length; i++ ) {
                    VsqEvent ve = items[i];
                    for ( int j = 0; j < Track.get( track ).getEventCount(); j++ ) {
                        VsqEvent ve2 = (VsqEvent)Track.get( track ).getEvent( j );
                        if ( ve2.InternalID == ve.InternalID ) {
                            reverse[i] = (VsqEvent)ve2.clone();
                            Track.get( track ).setEvent( j, items[i] );
                            break;
                        }
                    }
                }
                Track.get( track ).sortEvent();
                updateTotalClocks();
                ret = VsqCommand.generateCommandEventReplaceRange( track, reverse );
                return ret;
                #endregion
            }

            return null;
        }

        private void processTrackCurveEdit( int track, 
                                            String curve, 
                                            Vector<Long> delete,
                                            TreeMap<Integer, VsqBPPair> add,
                                            Vector<Long> inv_delete,
                                            TreeMap<Integer, VsqBPPair> inv_add ){
            VsqBPList list = Track.get( track ).getCurve( curve );

            // 現在の編集範囲を取得
            int edited_start = Track.get( track ).getEditedStart();
            int edited_end = Track.get( track ).getEditedEnd();

            // 逆コマンド発行用
            inv_delete.clear();
            inv_add.clear();

            // 最初に削除コマンドを実行
            for ( Iterator itr = delete.iterator(); itr.hasNext(); ) {
                long id = (Long)itr.next();
                VsqBPPairSearchContext item = list.findElement( id );
                if ( item.index >= 0 ) {
                    int clock = item.clock;
                    list.removeElementAt( item.index );
                    inv_add.put( clock, new VsqBPPair( item.point.value, item.point.id ) );
                    edited_start = Math.Min( edited_start, clock );
                    edited_end = Math.Max( edited_end, clock );
                }
            }

            // 追加コマンドを実行
            for ( Iterator itr = add.keySet().iterator(); itr.hasNext(); ) {
                int clock = (Integer)itr.next();
                VsqBPPair item = add.get( clock );
                list.addWithID( clock, item.value, item.id );
                inv_delete.add( item.id );
                edited_start = Math.Min( edited_start, clock );
                edited_end = Math.Max( edited_end, clock );
            }

            // 編集範囲の更新
            Track.get( track ).setEditedStart( edited_start );
            Track.get( track ).setEditedEnd( edited_end );
        }

        /// <summary>
        /// VSQファイルの指定されたクロック範囲のイベント等を削除します
        /// </summary>
        /// <param name="vsq">編集対象のVsqFileインスタンス</param>
        /// <param name="clock_start">削除を行う範囲の開始クロック</param>
        /// <param name="clock_end">削除を行う範囲の終了クロック</param>
        public void removePart( int clock_start, int clock_end ) {
            int dclock = clock_end - clock_start;

            // テンポ情報の削除、シフト
            boolean changed = true;
            Vector<TempoTableEntry> buf = new Vector<TempoTableEntry>( TempoTable );
            int tempo_at_clock_start = getTempoAt( clock_start );
            int tempo_at_clock_end = getTempoAt( clock_end );
            TempoTable.clear();
            boolean just_on_clock_end_added = false;
            for ( int i = 0; i < buf.size(); i++ ) {
                if ( buf.get( i ).Clock < clock_start ) {
                    TempoTable.add( (TempoTableEntry)buf.get( i ).clone() );
                } else if ( clock_end <= buf.get( i ).Clock ) {
                    TempoTableEntry tte = (TempoTableEntry)buf.get( i ).clone();
                    tte.Clock = tte.Clock - dclock;
                    if ( clock_end == buf.get( i ).Clock ) {
                        TempoTable.add( tte );
                        just_on_clock_end_added = true;
                    } else {
                        if ( tempo_at_clock_start != tempo_at_clock_end ) {
                            if ( !just_on_clock_end_added ) {
                                TempoTable.add( new TempoTableEntry( clock_start, tempo_at_clock_end, 0.0 ) );
                                just_on_clock_end_added = true;
                            }
                        }
                        TempoTable.add( tte );
                    }
                }
            }
            if ( tempo_at_clock_start != tempo_at_clock_end && !just_on_clock_end_added ) {
                TempoTable.add( new TempoTableEntry( clock_start, tempo_at_clock_end, 0.0 ) );
            }
            updateTempoInfo();

            for ( int track = 1; track < Track.size(); track++ ) {
                // 削除する範囲に歌手変更イベントが存在するかどうかを検査。
                VsqEvent t_last_singer = null;
                for ( Iterator itr = Track.get( track ).getSingerEventIterator(); itr.hasNext(); ) {
                    VsqEvent ve = (VsqEvent)itr.next();
                    if ( clock_start <= ve.Clock && ve.Clock < clock_end ) {
                        t_last_singer = ve;
                    }
                    if ( ve.Clock == clock_end ) {
                        t_last_singer = null; // 後でclock_endの位置に補うが、そこにに既に歌手変更イベントがあるとまずいので。
                    }
                }
                VsqEvent last_singer = null;
                if ( t_last_singer != null ) {
                    last_singer = (VsqEvent)t_last_singer.clone();
                    last_singer.Clock = clock_end;
                }

                changed = true;
                // イベントの削除
                while ( changed ) {
                    changed = false;
                    for ( int i = 0; i < Track.get( track ).getEventCount(); i++ ) {
                        if ( clock_start <= Track.get( track ).getEvent( i ).Clock && Track.get( track ).getEvent( i ).Clock < clock_end ) {
                            Track.get( track ).removeEvent( i );
                            changed = true;
                            break;
                        }
                    }
                }

                // クロックのシフト
                if ( last_singer != null ) {
                    Track.get( track ).addEvent( last_singer ); //歌手変更イベントを補う
                }
                for ( int i = 0; i < Track.get( track ).getEventCount(); i++ ) {
                    if ( clock_end <= Track.get( track ).getEvent( i ).Clock ) {
                        Track.get( track ).getEvent( i ).Clock -= dclock;
                    }
                }

                for ( int i = 0; i < _CURVES.Length; i++ ) {
                    String curve = _CURVES[i];
                    if ( curve.Equals( "VEL" ) ) {
                        continue;
                    }
                    VsqBPList buf_bplist = (VsqBPList)Track.get( track ).getCurve( curve ).clone();
                    Track.get( track ).getCurve( curve ).clear();
                    int value_at_end = buf_bplist.getValue( clock_end );
                    boolean at_end_added = false;
                    for ( Iterator itr = buf_bplist.keyClockIterator(); itr.hasNext(); ) {
                        int key = (Integer)itr.next();
                        if ( key < clock_start ) {
                            Track.get( track ).getCurve( curve ).add( key, buf_bplist.getValue( key ) );
                        } else if ( clock_end <= key ) {
                            if ( key == clock_end ) {
                                at_end_added = true;
                            }
                            Track.get( track ).getCurve( curve ).add( key - dclock, buf_bplist.getValue( key ) );
                        }
                    }
                    if ( !at_end_added ) {
                        Track.get( track ).getCurve( curve ).add( clock_end - dclock, value_at_end );
                    }
                }
            }
        }

        /// <summary>
        /// vsqファイル全体のイベントを，指定したクロックだけ遅らせます．
        /// ただし，曲頭のテンポ変更イベントと歌手変更イベントはクロック0から移動しません．
        /// この操作を行うことで，TimesigTableの情報は破綻します（仕様です）．
        /// </summary>
        /// <param name="delta_clock"></param>
        public static void shift( VsqFile vsq, int delta_clock ) {
            if ( delta_clock == 0 ) {
                return;
            }
            int dclock = (int)delta_clock;
            for ( int i = 0; i < vsq.TempoTable.size(); i++ ) {
                if ( vsq.TempoTable.get( i ).Clock > 0 ) {
                    vsq.TempoTable.get( i ).Clock = vsq.TempoTable.get( i ).Clock + dclock;
                }
            }
            vsq.updateTempoInfo();
            for ( int track = 1; track < vsq.Track.size(); track++ ) {
                for ( int i = 0; i < vsq.Track.get( track ).getEventCount(); i++ ) {
                    if ( vsq.Track.get( track ).getEvent( i ).Clock > 0 ) {
                        vsq.Track.get( track ).getEvent( i ).Clock += dclock;
                    }
                }
                for ( int i = 0; i < _CURVES.Length; i++ ) {
                    String curve = _CURVES[i];
                    if ( curve.Equals( "VEL" ) ) {
                        continue;

                    }
                    // 順番に+=dclockしていくとVsqBPList内部のSortedListの値がかぶる可能性がある．
                    VsqBPList edit = vsq.Track.get( track ).getCurve( curve );
                    VsqBPList new_one = new VsqBPList( edit.getName(), edit.getDefault(), edit.getMinimum(), edit.getMaximum() );
                    for ( Iterator itr2 = edit.keyClockIterator(); itr2.hasNext(); ) {
                        int key = (Integer)itr2.next();
                        new_one.add( key + dclock, edit.getValue( key ) );
                    }
                    vsq.Track.get( track ).setCurve( curve, new_one );
                }
            }
            vsq.updateTotalClocks();
        }

        /// <summary>
        /// このインスタンスのコピーを作成します
        /// </summary>
        /// <returns>このインスタンスのコピー</returns>
        public Object clone() {
            VsqFile ret = new VsqFile();
            ret.Track = new Vector<VsqTrack>();
            for ( int i = 0; i < Track.size(); i++ ) {
                ret.Track.add( (VsqTrack)Track.get( i ).clone() );
            }

            ret.TempoTable = new Vector<TempoTableEntry>();
            for ( int i = 0; i < TempoTable.size(); i++ ) {
                ret.TempoTable.add( (TempoTableEntry)TempoTable.get( i ).clone() );
            }

            ret.TimesigTable = new Vector<TimeSigTableEntry>();
            for ( int i = 0; i < TimesigTable.size(); i++ ) {
                ret.TimesigTable.add( (TimeSigTableEntry)TimesigTable.get( i ).clone() );
            }
            ret.m_tpq = m_tpq;
            ret.TotalClocks = TotalClocks;
            ret.m_base_tempo = m_base_tempo;
            ret.Master = (VsqMaster)Master.clone();
            ret.Mixer = (VsqMixer)Mixer.clone();
            //ret.m_premeasure_clocks = m_premeasure_clocks;
            return ret;
        }

#if !JAVA
        public object Clone() {
            return clone();
        }
#endif

        private VsqFile() {
        }

#if JAVA
        private class BarLineIterator implements Iterator{
#else
        private class BarLineIterator : Iterator {
#endif
            private Vector<TimeSigTableEntry> m_list;
            private int m_end_clock;
            private int i;
            private int clock;
            int local_denominator;
            int local_numerator;
            int clock_step;
            int t_end;
            int local_clock;
            int bar_counter;

            public BarLineIterator( Vector<TimeSigTableEntry> list, int end_clock ) {
                m_list = list;
                m_end_clock = end_clock;
                i = 0;
                t_end = -1;
                clock = 0;
            }

            public Object next() {
                int mod = clock_step * local_numerator;
                if ( clock < t_end ) {
                    if ( (clock - local_clock) % mod == 0 ) {
                        bar_counter++;
                        VsqBarLineType ret = new VsqBarLineType( clock, true, local_denominator, local_numerator, bar_counter );
                        clock += clock_step;
                        return ret;
                    } else {
                        VsqBarLineType ret = new VsqBarLineType( clock, false, local_denominator, local_numerator, bar_counter );
                        clock += clock_step;
                        return ret;
                    }
                }

                if ( i < m_list.size() ) {
                    local_denominator = m_list.get( i ).Denominator;
                    local_numerator = m_list.get( i ).Numerator;
                    local_clock = m_list.get( i ).Clock;
                    int local_bar_count = m_list.get( i ).BarCount;
                    clock_step = 480 * 4 / local_denominator;
                    mod = clock_step * local_numerator;
                    bar_counter = local_bar_count - 1;
                    t_end = m_end_clock;
                    if ( i + 1 < m_list.size() ) {
                        t_end = m_list.get( i + 1 ).Clock;
                    }
                    i++;
                    clock = local_clock;
                    if ( clock < t_end ) {
                        if ( (clock - local_clock) % mod == 0 ) {
                            bar_counter++;
                            VsqBarLineType ret = new VsqBarLineType( clock, true, local_denominator, local_numerator, bar_counter );
                            clock += clock_step;
                            return ret;
                        } else {
                            VsqBarLineType ret = new VsqBarLineType( clock, false, local_denominator, local_numerator, bar_counter );
                            clock += clock_step;
                            return ret;
                        }
                    }
                }
                return null;
            }

            public void remove() {
                //throw new Exception( "com.boare.vsq.VsqFile.BarLineIterator#remove; not implemented" );
            }

            public boolean hasNext() {
                if ( clock < m_end_clock ) {
                    return true;
                } else {
                    return false;
                }
            }
        }

        /// <summary>
        /// 小節の区切りを順次返すIterator。
        /// </summary>
        /// <returns></returns>
        public Iterator getBarLineIterator( int end_clock ) {
            return new BarLineIterator( TimesigTable, end_clock );
        }

        /// <summary>
        /// 基本テンポ値を取得します
        /// </summary>
        public int getBaseTempo() {
            return m_base_tempo;
        }

        /// <summary>
        /// プリメジャー値を取得します
        /// </summary>
        public int getPreMeasure() {
            return Master.PreMeasure;
        }

        /// <summary>
        /// プリメジャー部分の長さをクロックに変換した値を取得します．
        /// </summary>
        public int getPreMeasureClocks() {
            return calculatePreMeasureInClock();
        }

        /// <summary>
        /// プリメジャーの長さ(クロック)を計算します。
        /// </summary>
        private int calculatePreMeasureInClock() {
            int pre_measure = Master.PreMeasure;
            int last_bar_count = TimesigTable.get( 0 ).BarCount;
            int last_clock = TimesigTable.get( 0 ).Clock;
            int last_denominator = TimesigTable.get( 0 ).Denominator;
            int last_numerator = TimesigTable.get( 0 ).Numerator;
            for ( int i = 1; i < TimesigTable.size(); i++ ) {
                if ( TimesigTable.get( i ).BarCount >= pre_measure ) {
                    break;
                } else {
                    last_bar_count = TimesigTable.get( i ).BarCount;
                    last_clock = TimesigTable.get( i ).Clock;
                    last_denominator = TimesigTable.get( i ).Denominator;
                    last_numerator = TimesigTable.get( i ).Numerator;
                }
            }

            int remained = pre_measure - last_bar_count;//プリメジャーの終わりまでの残り小節数
            return last_clock + remained * last_numerator * 480 * 4 / last_denominator;
        }

        /// <summary>
        /// 指定したクロックにおける、clock=0からの演奏経過時間(sec)を取得します
        /// </summary>
        /// <param name="clock"></param>
        /// <returns></returns>
        public double getSecFromClock( double clock ) {
            int c = TempoTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                TempoTableEntry item = TempoTable.get( i );
                if ( item.Clock < clock ) {
                    double init = item.Time;
                    double dclock = clock - item.Clock;
                    double sec_per_clock1 = item.Tempo * 1e-6 / 480.0;
                    return init + dclock * sec_per_clock1;
                }
            }

            double sec_per_clock = m_base_tempo * 1e-6 / 480.0;
            return clock * sec_per_clock;
        }

        /// <summary>
        /// 指定した時刻における、クロックを取得します
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public double getClockFromSec( double time ) {
            // timeにおけるテンポを取得
            int tempo = m_base_tempo;
            double base_clock = 0;
            double base_time = 0f;
            int c = TempoTable.size();
            if ( c == 0 ) {
                tempo = m_base_tempo;
                base_clock = 0;
                base_time = 0f;
            } else if ( c == 1 ) {
                tempo = TempoTable.get( 0 ).Tempo;
                base_clock = TempoTable.get( 0 ).Clock;
                base_time = TempoTable.get( 0 ).Time;
            } else {
                for ( int i = c - 1; i >= 0; i-- ) {
                    TempoTableEntry item = TempoTable.get( i );
                    if ( item.Time < time ) {
                        return item.Clock + (time - item.Time) * m_tpq * 1000000.0 / item.Tempo;
                    }
                }
            }
            double dt = time - base_time;
            return base_clock + dt * m_tpq * 1000000.0 / (double)tempo;
        }

        /// <summary>
        /// 指定したクロックにおける拍子を取得します
        /// </summary>
        /// <param name="clock"></param>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        public Timesig getTimesigAt( int clock ) {
            Timesig ret = new Timesig();
            ret.numerator = 4;
            ret.denominator = 4;
            int index = 0;
            int c = TimesigTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                index = i;
                if ( TimesigTable.get( i ).Clock <= clock ) {
                    break;
                }
            }
            ret.numerator = TimesigTable.get( index ).Numerator;
            ret.denominator = TimesigTable.get( index ).Denominator;
            return ret;
        }

        public Timesig getTimesigAt( int clock, ByRef<Integer> bar_count ) {
            int index = 0;
            int c = TimesigTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                index = i;
                if ( TimesigTable.get( i ).Clock <= clock ) {
                    break;
                }
            }
            TimeSigTableEntry item = TimesigTable.get( index );
            Timesig ret = new Timesig();
            ret.numerator = item.Numerator;
            ret.denominator = item.Denominator;
            int diff = clock - item.Clock;
            int clock_per_bar = 480 * 4 / ret.denominator * ret.numerator;
            bar_count.value = item.BarCount + diff / clock_per_bar;
            return ret;
        }

        /// <summary>
        /// 指定したクロックにおけるテンポを取得します。
        /// </summary>
        /// <param name="clock"></param>
        /// <returns></returns>
        public int getTempoAt( int clock ) {
            int index = 0;
            int c = TempoTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                index = i;
                if ( TempoTable.get( i ).Clock <= clock ) {
                    break;
                }
            }
            return TempoTable.get( index ).Tempo;
        }

        /// <summary>
        /// 指定した小節の開始クロックを調べます。ここで使用する小節数は、プリメジャーを考慮しない。即ち、曲頭の小節が0である。
        /// </summary>
        /// <param name="bar_count"></param>
        /// <returns></returns>
        public int getClockFromBarCount( int bar_count ) {
            int index = 0;
            int c = TimesigTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                index = i;
                if ( TimesigTable.get( i ).BarCount <= bar_count ) {
                    break;
                }
            }
            TimeSigTableEntry item = TimesigTable.get( index );
            int numerator = item.Numerator;
            int denominator = item.Denominator;
            int init_clock = item.Clock;
            int init_bar_count = item.BarCount;
            int clock_per_bar = numerator * 480 * 4 / denominator;
            return init_clock + (bar_count - init_bar_count) * clock_per_bar;
        }

        /// <summary>
        /// 指定したクロックが、曲頭から何小節目に属しているかを調べます。ここで使用する小節数は、プリメジャーを考慮しない。即ち、曲頭の小節が0である。
        /// </summary>
        /// <param name="clock"></param>
        /// <returns></returns>
        public int getBarCountFromClock( int clock ) {
            int index = 0;
            int c = TimesigTable.size();
            for ( int i = c - 1; i >= 0; i-- ) {
                index = i;
                if ( TimesigTable.get( i ).Clock <= clock ) {
                    break;
                }
            }
            int bar_count = 0;
            if ( index >= 0 ) {
                int last_clock = TimesigTable.get( index ).Clock;
                int t_bar_count = TimesigTable.get( index ).BarCount;
                int numerator = TimesigTable.get( index ).Numerator;
                int denominator = TimesigTable.get( index ).Denominator;
                int clock_per_bar = numerator * 480 * 4 / denominator;
                bar_count = t_bar_count + (clock - last_clock) / clock_per_bar;
            }
            return bar_count;
        }

        /// <summary>
        /// 4分の1拍子1音あたりのクロック数を取得します
        /// </summary>
        public int getTickPerQuarter() {
            return m_tpq;
        }

        /// <summary>
        /// 空のvsqファイルを構築します
        /// </summary>
        /// <param name="pre_measure"></param>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        /// <param name="tempo"></param>
        public VsqFile( String singer, int pre_measure, int numerator, int denominator, int tempo ) {
            TotalClocks = pre_measure * 480 * 4 / denominator * numerator;
            m_tpq = 480;

            Track = new Vector<VsqTrack>();
            Track.add( new VsqTrack( tempo, numerator, denominator ) );
            Track.add( new VsqTrack( "Voice1", singer ) );
            Master = new VsqMaster( pre_measure );
#if DEBUG
            PortUtil.println( "VsqFile.ctor()" );
#endif
            Mixer = new VsqMixer( 0, 0, 0, 0 );
            Mixer.Slave.add( new VsqMixerEntry( 0, 0, 0, 0 ) );
            TimesigTable = new Vector<TimeSigTableEntry>();
            TimesigTable.add( new TimeSigTableEntry( 0, numerator, denominator, 0 ) );
            TempoTable = new Vector<TempoTableEntry>();
            TempoTable.add( new TempoTableEntry( 0, tempo, 0.0 ) );
            m_base_tempo = tempo;
            //m_premeasure_clocks = calculatePreMeasureInClock();
        }

        /// <summary>
        /// vsqファイルからのコンストラクタ
        /// </summary>
        /// <param name="_fpath"></param>
        public VsqFile( String _fpath, String encoding )
#if JAVA
            throws FileNotFoundException
#endif
 {
            TempoTable = new Vector<TempoTableEntry>();
            TimesigTable = new Vector<TimeSigTableEntry>();
            m_tpq = 480;

            // SMFをコンバートしたテキストファイルを作成
            MidiFile mf = new MidiFile( _fpath );
            Track = new Vector<VsqTrack>();
            int num_track = mf.getTrackCount();
#if DEBUG
            PortUtil.println( "VsqFile#.ctor; num_track=" + num_track );
#endif
            for ( int i = 0; i < num_track; i++ ) {
                Track.add( new VsqTrack( mf.getMidiEventList( i ), encoding ) );
            }

            Master = (VsqMaster)Track.get( 1 ).getMaster().clone();
            Mixer = (VsqMixer)Track.get( 1 ).getMixer().clone();
            Track.get( 1 ).setMaster( null );
            Track.get( 1 ).setMixer( null );

#if DEBUG
            System.Diagnostics.Debug.WriteLine( "VsqFile.ctor()" );
#endif
            int master_track = -1;
            for ( int i = 0; i < Track.size(); i++ ) {
#if DEBUG
                System.Diagnostics.Debug.WriteLine( "    m_tracks[i].Name=" + Track.get( i ).getName() );
#endif
                if ( Track.get( i ).getName().Equals( "Master Track" ) ) {
                    master_track = i;
                    break;
                }
            }

            int prev_tempo;
            int prev_index;
            double prev_time;
            if ( master_track >= 0 ) {
                #region TempoListの作成
                // MIDI event リストの取得
                Vector<MidiEvent> midi_event = mf.getMidiEventList( master_track );//.TempoTable;
                // とりあえずtempo_tableに格納
                m_base_tempo = 500000;
                prev_tempo = 500000;
                prev_index = 0;
                double thistime;
                prev_time = 0.0;
                int count = -1;
                int midi_event_size = midi_event.size();
                for ( int j = 0; j < midi_event_size; j++ ) {
                    MidiEvent itemj = midi_event.get( j );
                    if ( itemj.firstByte == (byte)0xff && itemj.data.Length >= 4 && itemj.data[0] == (byte)0x51 ) {
                        count++;
                        if ( count == 0 && itemj.clock != 0 ) {
                            TempoTable.add( new TempoTableEntry( 0, 500000, 0.0 ) );
                            m_base_tempo = 500000;
                            prev_tempo = 500000;
                        }
                        int current_tempo = itemj.data[1] << 16 | itemj.data[2] << 8 | itemj.data[3];
                        int current_index = (int)midi_event.get( j ).clock;
                        thistime = prev_time + (double)(prev_tempo) * (double)(current_index - prev_index) / (m_tpq * 1000000.0);
                        TempoTable.add( new TempoTableEntry( current_index, current_tempo, thistime ) );
                        prev_tempo = current_tempo;
                        prev_index = current_index;
                        prev_time = thistime;
                    }
                }
                Collections.sort( TempoTable );
                #endregion

                #region TimeSigTableの作成
                //Vector<MidiEvent> time_sigs = mf.getMidiEventList( master_track );//].TimesigTable;
                int dnomi = 4;// time_sigs[0].Data[1];
                int numer = 4;
                /*for ( int i = 0; i < time_sigs[0].Data[2]; i++ ) {
                    numer = numer * 2;
                }*/
                //m_timesig_table.Add( new TimeSigTableEntry( 0, numer, dnomi, 0 ) );
                count = -1;
                for ( int j = 0; j < midi_event.size(); j++ ) {
                    if ( midi_event.get( j ).firstByte == (byte)0xff && midi_event.get( j ).data.Length >= 5 && midi_event.get( j ).data[0] == (byte)0x58 ) {
                        count++;
                        numer = midi_event.get( j ).data[1];
                        dnomi = 1;
                        for ( int i = 0; i < midi_event.get( j ).data[2]; i++ ) {
                            dnomi = dnomi * 2;
                        }
                        if ( count == 0 ) {
                            int numerator = 4;
                            int denominator = 4;
                            int clock = 0;
                            int bar_count = 0;
                            if ( midi_event.get( j ).clock == 0 ) {
                                TimesigTable.add( new TimeSigTableEntry( 0, numer, dnomi, 0 ) );
                            } else {
                                TimesigTable.add( new TimeSigTableEntry( 0, 4, 4, 0 ) );
                                TimesigTable.add( new TimeSigTableEntry( 0, numer, dnomi, (int)midi_event.get( j ).clock / (480 * 4) ) );
                                count++;
                            }
                        } else {
                            int numerator = TimesigTable.get( count - 1 ).Numerator;
                            int denominator = TimesigTable.get( count - 1 ).Denominator;
                            int clock = TimesigTable.get( count - 1 ).Clock;
                            int bar_count = TimesigTable.get( count - 1 ).BarCount;
                            int dif = 480 * 4 / denominator * numerator;//1小節が何クロックか？
                            bar_count += ((int)midi_event.get( j ).clock - clock) / dif;
                            TimesigTable.add( new TimeSigTableEntry( (int)midi_event.get( j ).clock, numer, dnomi, bar_count ) );
                        }
                    }
                }
                #endregion
            }

            // 曲の長さを計算
            updateTempoInfo();
            updateTimesigInfo();
            //m_premeasure_clocks = calculatePreMeasureInClock();
            updateTotalClocks();
#if DEBUG
            System.Diagnostics.Debug.WriteLine( "    m_total_clocks=" + TotalClocks );
#endif
        }

        /// <summary>
        /// TimeSigTableの[*].Clockの部分を更新します
        /// </summary>
        public void updateTimesigInfo() {
#if DEBUG
            PortUtil.println( "VsqFile.UpdateTimesigInfo()" );
#endif
            if ( TimesigTable.get( 0 ).Clock != 0 ) {
                return;
            }
            TimesigTable.get( 0 ).Clock = 0;
            Collections.sort( TimesigTable );
            int count = TimesigTable.size();
            for ( int j = 1; j < count; j++ ) {
                TimeSigTableEntry item = TimesigTable.get( j - 1 );
                int numerator = item.Numerator;
                int denominator = item.Denominator;
                int clock = item.Clock;
                int bar_count = item.BarCount;
                int dif = 480 * 4 / denominator * numerator;//1小節が何クロックか？
                clock += (TimesigTable.get( j ).BarCount - bar_count) * dif;
                TimesigTable.get( j ).Clock = clock;
            }
#if DEBUG
            PortUtil.println( "TimesigTable;" );
            for ( int i = 0; i < TimesigTable.size(); i++ ) {
                PortUtil.println( "    " + TimesigTable.get( i ).Clock + " " + TimesigTable.get( i ).Numerator + "/" + TimesigTable.get( i ).Denominator );
            }
#endif
        }

        /// <summary>
        /// TempoTableの[*].Timeの部分を更新します
        /// </summary>
        public void updateTempoInfo() {
            if ( TempoTable.size() == 0 ) {
                TempoTable.add( new TempoTableEntry( 0, getBaseTempo(), 0.0 ) );
            }
            Collections.sort( TempoTable );
            if ( TempoTable.get( 0 ).Clock != 0 ) {
                TempoTable.get( 0 ).Time = (double)getBaseTempo() * (double)TempoTable.get( 0 ).Clock / (getTickPerQuarter() * 1000000.0);
            } else {
                TempoTable.get( 0 ).Time = 0.0;
            }
            double prev_time = TempoTable.get( 0 ).Time;
            int prev_clock = TempoTable.get( 0 ).Clock;
            int prev_tempo = TempoTable.get( 0 ).Tempo;
            double inv_tpq_sec = 1.0 / (getTickPerQuarter() * 1000000.0);
            for ( int i = 1; i < TempoTable.size(); i++ ) {
                TempoTable.get( i ).Time = prev_time + (double)prev_tempo * (double)(TempoTable.get( i ).Clock - prev_clock) * inv_tpq_sec;
                prev_time = TempoTable.get( i ).Time;
                prev_tempo = TempoTable.get( i ).Tempo;
                prev_clock = TempoTable.get( i ).Clock;
            }
        }

        /// <summary>
        /// VsqFile.Executeの実行直後などに、m_total_clocksの値を更新する
        /// </summary>
        public void updateTotalClocks() {
            int max = getPreMeasureClocks();
            for ( int i = 1; i < Track.size(); i++ ) {
                VsqTrack track = Track.get( i );
                for ( Iterator itr = track.getEventIterator(); itr.hasNext(); ) {
                    VsqEvent ve = (VsqEvent)itr.next();
                    max = Math.Max( max, ve.Clock + ve.ID.getLength() );
                }
                for ( int j = 0; j < _CURVES.Length; j++ ) {
                    String vct = _CURVES[j];
                    if ( vct.Equals( "VEL" ) ) {
                        continue;
                    }
                    VsqBPList list = track.getCurve( vct );
                    if ( list != null && list.size() > 0 ) {
                        int keys = list.size();
                        int last_key = list.getKeyClock( keys - 1 );
                        max = Math.Max( max, last_key );
                    }
                }
            }
            TotalClocks = max;
        }

        /// <summary>
        /// 曲の長さを取得する。(sec)
        /// </summary>
        public double getTotalSec() {
            return getSecFromClock( (int)TotalClocks );
        }

        /// <summary>
        /// 指定された番号のトラックに含まれる歌詞を指定されたファイルに出力します
        /// </summary>
        /// <param name="track"></param>
        /// <param name="fpath"></param>
        public void printLyricTable( int track, String fpath ) {
            BufferedWriter sw = null;
            try {
                sw = new BufferedWriter( new FileWriter( fpath ) );
                for ( int i = 0; i < Track.get( track ).getEventCount(); i++ ) {
                    int Length;
                    // timesignal
                    int time_signal = Track.get( track ).getEvent( i ).Clock;
                    // イベントで指定されたIDがLyricであった場合
                    if ( Track.get( track ).getEvent( i ).ID.type == VsqIDType.Anote ) {
                        // 発音長を取得
                        Length = Track.get( track ).getEvent( i ).ID.getLength();

                        // tempo_tableから、発音開始時のtempoを取得
                        int last = TempoTable.size() - 1;
                        int tempo = TempoTable.get( last ).Tempo;
                        int prev_index = TempoTable.get( last ).Clock;
                        double prev_time = TempoTable.get( last ).Time;
                        for ( int j = 1; j < TempoTable.size(); j++ ) {
                            if ( TempoTable.get( j ).Clock > time_signal ) {
                                tempo = TempoTable.get( j - 1 ).Tempo;
                                prev_index = TempoTable.get( j - 1 ).Clock;
                                prev_time = TempoTable.get( j - 1 ).Time;
                                break;
                            }
                        }
                        int current_index = Track.get( track ).getEvent( i ).Clock;
                        double start_time = prev_time + (double)(current_index - prev_index) * (double)tempo / (m_tpq * 1000000.0);
                        // TODO: 単純に + Lengthしただけではまずいはず。要検討
                        double end_time = start_time + ((double)Length) * ((double)tempo) / (m_tpq * 1000000.0);
                        sw.write( Track.get( track ).getEvent( i ).Clock + "," +
                                  PortUtil.formatDecimal( "0.000000", start_time ) + "," +
                                  PortUtil.formatDecimal( "0.000000", end_time ) + "," +
                                  Track.get( track ).getEvent( i ).ID.LyricHandle.L0.Phrase + "," +
                                  Track.get( track ).getEvent( i ).ID.LyricHandle.L0.getPhoneticSymbol() );
                        sw.newLine();
                    }

                }
            } catch ( Exception ex ) {
            } finally {
                if ( sw != null ) {
                    try {
                        sw.close();
                    } catch ( Exception ex2 ) {
                    }
                }
            }
        }

        public Vector<MidiEvent> generateMetaTextEvent( int track, String encoding ) {
            String _NL = "" + (char)(byte)0x0a;
            Vector<MidiEvent> ret = new Vector<MidiEvent>();
            TextMemoryStream sr = null;
            try {
                sr = new TextMemoryStream();
                Track.get( track ).printMetaText( sr, TotalClocks + 120, calculatePreMeasureInClock() );
                sr.rewind();
                int line_count = -1;
                String tmp = "";
                if ( sr.peek() >= 0 ) {
#if NEW_IMPL
                    Vector<Byte> buffer = new Vector<Byte>();
                    boolean first = true;
                    while ( sr.peek() >= 0 ) {
                        if ( first ) {
                            tmp = sr.readLine();
                            first = false;
                        } else {
                            tmp = _NL + sr.readLine();
                        }
                        Byte[] linebytes = PortUtil.convertByteArray( PortUtil.getEncodedByte( encoding, tmp ) );
                        buffer.addAll( Arrays.asList( linebytes ) );
                        while ( getLinePrefixBytes( line_count + 1 ).Length + buffer.size() >= 127 ) {
                            line_count++;
                            byte[] prefix = getLinePrefixBytes( line_count );
                            MidiEvent add = new MidiEvent();
                            add.clock = 0;
                            add.firstByte = (byte)0xff;
                            add.data = new byte[128];
                            add.data[0] = (byte)0x01;
                            int remain = 127;
                            for ( int i = 0; i < prefix.Length; i++ ) {
                                add.data[i + 1] = prefix[i];
                            }
                            for ( int i = prefix.Length; i < remain; i++ ) {
                                byte d = buffer.get( 0 );
                                add.data[i + 1] = d;
                                buffer.removeElementAt( 0 );
                            }
                            ret.add( add );
                        }
                    }
                    if ( buffer.size() > 0 ) {
                        while ( getLinePrefixBytes( line_count + 1 ).Length + buffer.size() >= 127 ) {
                            line_count++;
                            byte[] prefix = getLinePrefixBytes( line_count );
                            MidiEvent add = new MidiEvent();
                            add.clock = 0;
                            add.firstByte = (byte)0xff;
                            add.data = new byte[128];
                            add.data[0] = (byte)0x01;
                            int remain = 127;
                            for ( int i = 0; i < prefix.Length; i++ ) {
                                add.data[i + 1] = prefix[i];
                            }
                            for ( int i = prefix.Length; i < remain; i++ ) {
                                add.data[i + 1] = buffer.get( 0 );
                                buffer.removeElementAt( 0 );
                            }
                            ret.add( add );
                        }
                        if ( buffer.size() > 0 ) {
                            line_count++;
                            byte[] prefix = getLinePrefixBytes( line_count );
                            MidiEvent add = new MidiEvent();
                            add.clock = 0;
                            add.firstByte = (byte)0xff;
                            int remain = prefix.Length + buffer.size();
                            add.data = new byte[remain + 1];
                            add.data[0] = (byte)0x01;
                            for ( int i = 0; i < prefix.Length; i++ ) {
                                add.data[i + 1] = prefix[i];
                            }
                            for ( int i = prefix.Length; i < remain; i++ ) {
                                add.data[i + 1] = buffer.get( 0 );
                                buffer.removeElementAt( 0 );
                            }
                            ret.add( add );
                        }
                    }
#else
                    tmp = sr.readLine();
                    byte[] line_bytes;
                    while ( sr.peek() >= 0 ) {
                        tmp += _NL + sr.readLine();
                        while ( PortUtil.getEncodedByteCount( encoding, tmp + getLinePrefix( line_count + 1 ) ) >= 127 ) {
                            line_count++;
                            tmp = getLinePrefix( line_count ) + tmp;
                            String work = substring127Bytes( tmp, encoding );// tmp.Substring( 0, 127 );
                            tmp = tmp.Substring( PortUtil.getStringLength( work ) );
                            line_bytes = PortUtil.getEecodedByte( encoding, work );
                            MidiEvent add = new MidiEvent();
                            add.clock = 0;
                            add.firstByte = (byte)0xff; //ステータス メタ＊
                            add.data = new byte[line_bytes.Length + 1];
                            add.data[0] = (byte)0x01; //メタテキスト
                            for ( int i = 0; i < line_bytes.Length; i++ ) {
                                add.data[i + 1] = line_bytes[i];
                            }
                            ret.add( add );
                        }
                    }
                    // 残りを出力
                    line_count++;
                    tmp = getLinePrefix( line_count ) + tmp + _NL;
                    while ( PortUtil.getEncodedByteCount( encoding, tmp ) > 127 ) {
                        String work = substring127Bytes( tmp, encoding );
                        tmp = tmp.Substring( PortUtil.getStringLength( work ) );
                        line_bytes = PortUtil.getEecodedByte( encoding, work );
                        MidiEvent add = new MidiEvent();
                        add.clock = 0;
                        add.firstByte = (byte)0xff;
                        add.data = new byte[line_bytes.Length + 1];
                        add.data[0] = (byte)0x01;
                        for ( int i = 0; i < line_bytes.Length; i++ ) {
                            add.data[i + 1] = line_bytes[i];
                        }
                        ret.add( add );
                        line_count++;
                        tmp = getLinePrefix( line_count );
                    }
                    line_bytes = PortUtil.getEecodedByte( encoding, tmp );
                    MidiEvent add0 = new MidiEvent();
                    add0.firstByte = (byte)0xff;
                    add0.data = new byte[line_bytes.Length + 1];
                    add0.data[0] = (byte)0x01;
                    for ( int i = 0; i < line_bytes.Length; i++ ) {
                        add0.data[i + 1] = line_bytes[i];
                    }
                    ret.add( add0 );
#endif
                }
            } catch ( Exception ex ) {
            } finally {
                if ( sr != null ) {
                    try {
                        sr.close();
                    } catch ( Exception ex2 ) {
                    }
                }
            }
#if DEBUG
            Console.WriteLine( "VsqFile#generateMetaTextEvent; ret.size()=" + ret.size() );
#endif
            return ret;
        }

        /// <summary>
        /// 文字列sの先頭から文字列を切り取るとき，切り取った文字列をencodingによりエンコードした結果が127Byte以下になるように切り取ります．
        /// </summary>
        /// <param name="s"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private static String substring127Bytes( String s, String encoding ) {
            int count = Math.Min( 127, PortUtil.getStringLength( s ) );
            int c = PortUtil.getEncodedByteCount( encoding, s.Substring( 0, count ) );
            if ( c == 127 ) {
                return s.Substring( 0, count );
            }
            int delta = c > 127 ? -1 : 1;
            while ( (delta == -1 && c > 127) || (delta == 1 && c < 127) ) {
                count += delta;
                if ( delta == -1 && count == 0 ) {
                    break;
                } else if ( delta == 1 && count == PortUtil.getStringLength( s ) ) {
                    break;
                }
                c = PortUtil.getEncodedByteCount( encoding, s.Substring( 0, count ) );
            }
            return s.Substring( 0, count );
        }

        private static void printTrack( VsqFile vsq, int track, RandomAccessFile fs, int msPreSend, String encoding )
#if JAVA
            throws IOException
#endif
 {
#if DEBUG
            PortUtil.println( "PrintTrack" );
#endif
            //VsqTrack item = Tracks[track];
            String _NL = "" + (char)(byte)0x0a;
            //ヘッダ
            fs.write( _MTRK, 0, 4 );
            //データ長。とりあえず0
            fs.write( new byte[] { (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00 }, 0, 4 );
            long first_position = fs.getFilePointer();
            //トラック名
            writeFlexibleLengthUnsignedLong( fs, (byte)0x00 );//デルタタイム
            fs.write( (byte)0xff );//ステータスタイプ
            fs.write( (byte)0x03 );//イベントタイプSequence/Track Name
            byte[] seq_name = PortUtil.getEncodedByte( encoding, vsq.Track.get( track ).getName() );
            writeFlexibleLengthUnsignedLong( fs, (long)seq_name.Length );//seq_nameの文字数
            fs.write( seq_name, 0, seq_name.Length );

            //Meta Textを準備
            Vector<MidiEvent> meta = vsq.generateMetaTextEvent( track, encoding );
            long lastclock = 0;
            for ( int i = 0; i < meta.size(); i++ ) {
                writeFlexibleLengthUnsignedLong( fs, (long)(meta.get( i ).clock - lastclock) );
                meta.get( i ).writeData( fs );
                lastclock = meta.get( i ).clock;
            }

            int last = 0;
            VsqNrpn[] data = generateNRPN( vsq, track, msPreSend );
            NrpnData[] nrpns = VsqNrpn.convert( data );
            for ( int i = 0; i < nrpns.Length; i++ ) {
                writeFlexibleLengthUnsignedLong( fs, (long)(nrpns[i].getClock() - last) );
                fs.write( (byte)0xb0 );
                fs.write( nrpns[i].getParameter() );
                fs.write( nrpns[i].Value );
                last = nrpns[i].getClock();
            }

            //トラックエンド
            VsqEvent last_event = vsq.Track.get( track ).getEvent( vsq.Track.get( track ).getEventCount() - 1 );
            int last_clock = last_event.Clock + last_event.ID.getLength();
            writeFlexibleLengthUnsignedLong( fs, (long)last_clock );
            fs.write( (byte)0xff );
            fs.write( (byte)0x2f );
            fs.write( (byte)0x00 );
            long pos = fs.getFilePointer();
            fs.seek( first_position - 4 );
            writeUnsignedInt( fs, (long)(pos - first_position) );
            fs.seek( pos );
        }

        /// <summary>
        /// 指定したクロックにおけるプリセンド・クロックを取得します
        /// </summary>
        /// <param name="clock"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public int getPresendClockAt( int clock, int msPreSend ) {
            double clock_msec = getSecFromClock( clock ) * 1000.0;
            float draft_clock_sec = (float)(clock_msec - msPreSend) / 1000.0f;
            int draft_clock = (int)Math.Floor( getClockFromSec( draft_clock_sec ) );
            return clock - draft_clock;
        }

        /// <summary>
        /// 指定したトラックから、Expression(DYN)のNRPNリストを作成します
        /// </summary>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateExpressionNRPN( VsqFile vsq, int track, int msPreSend ) {
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();
            VsqBPList dyn = vsq.Track.get( track ).getCurve( "DYN" );
            int count = dyn.size();
            for ( int i = 0; i < count; i++ ) {
                int clock = dyn.getKeyClock( i );
                int c = clock - vsq.getPresendClockAt( clock, msPreSend );
                if ( c >= 0 ) {
                    VsqNrpn add = new VsqNrpn( c,
                                               NRPN.CC_E_EXPRESSION,
                                               (byte)dyn.getElement( i ) );
                    ret.add( add );
                }
            }
            return ret.toArray( new VsqNrpn[] { } );
        }

        public static VsqNrpn[] generateFx2DepthNRPN( VsqFile vsq, int track, int msPreSend ) {
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();
            VsqBPList fx2depth = vsq.Track.get( track ).getCurve( "fx2depth" );
            int count = fx2depth.size();
            for ( int i = 0; i < count; i++ ) {
                int clock = fx2depth.getKeyClock( i );
                int c = clock - vsq.getPresendClockAt( clock, msPreSend );
                if ( c >= 0 ) {
                    VsqNrpn add = new VsqNrpn( c,
                                               NRPN.CC_FX2_EFFECT2_DEPTH,
                                               (byte)fx2depth.getElement( i ) );
                    ret.add( add );
                }
            }
            return ret.toArray( new VsqNrpn[] { } );
        }

        /// <summary>
        /// 先頭に記録されるNRPNを作成します
        /// </summary>
        /// <returns></returns>
        public static VsqNrpn generateHeaderNRPN() {
            VsqNrpn ret = new VsqNrpn( 0, NRPN.CC_BS_VERSION_AND_DEVICE, (byte)0x00, (byte)0x00 );
            ret.append( NRPN.CC_BS_DELAY, (byte)0x00, (byte)0x00 );
            ret.append( NRPN.CC_BS_LANGUAGE_TYPE, (byte)0x00 );
            return ret;
        }

        /// <summary>
        /// 歌手変更イベントから，NRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="ve"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateSingerNRPN( VsqFile vsq, VsqEvent ve, int msPreSend ) {
            int clock = ve.Clock;

            double clock_msec = vsq.getSecFromClock( clock ) * 1000.0;

            int ttempo = vsq.getTempoAt( clock );
            double tempo = 6e7 / ttempo;
            //double sStart = SecFromClock( ve.Clock );
            double msEnd = vsq.getSecFromClock( ve.Clock + ve.ID.getLength() ) * 1000.0;
            int duration = (int)Math.Ceiling( msEnd - clock_msec );
#if DEBUG
            PortUtil.println( "GenerateNoteNRPN" );
            PortUtil.println( "    duration=" + duration );
#endif
            ValuePair<Byte, Byte> d = getMsbAndLsb( duration );
            byte duration0 = d.getKey();
            byte duration1 = d.getValue();
            ValuePair<Byte, Byte> d2 = getMsbAndLsb( msPreSend );
            byte delay0 = d2.getKey();
            byte delay1 = d2.getValue();
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();

            int i = clock - vsq.getPresendClockAt( clock, msPreSend );
            VsqNrpn add = new VsqNrpn( i, NRPN.CC_BS_VERSION_AND_DEVICE, (byte)0x00, (byte)0x00 );
            add.append( NRPN.CC_BS_DELAY, delay0, delay1, true );
            add.append( NRPN.CC_BS_LANGUAGE_TYPE, (byte)ve.ID.IconHandle.Language, true );
            add.append( NRPN.PC_VOICE_TYPE, (byte)ve.ID.IconHandle.Program );
            return new VsqNrpn[] { add };
        }

        /// <summary>
        /// 音符イベントから，NRPNを作成します
        /// </summary>
        /// <param name="ve"></param>
        /// <param name="msPreSend"></param>
        /// <param name="note_loc"></param>
        /// <returns></returns>
        public static VsqNrpn generateNoteNRPN( VsqFile vsq, int track, VsqEvent ve, int msPreSend, byte note_loc, boolean add_delay_sign ) {
            int clock = ve.Clock;
            String renderer = vsq.Track.get( track ).getCommon().Version;

            double clock_msec = vsq.getSecFromClock( clock ) * 1000.0;

            int ttempo = vsq.getTempoAt( clock );
            double tempo = 6e7 / ttempo;
            double msEnd = vsq.getSecFromClock( ve.Clock + ve.ID.getLength() ) * 1000.0;
            int duration = (int)(msEnd - clock_msec);
            ValuePair<Byte, Byte> dur = getMsbAndLsb( duration );
            byte duration0 = dur.getKey();
            byte duration1 = dur.getValue();

            VsqNrpn add;
            if ( add_delay_sign ) {
                ValuePair<Byte, Byte> msp = getMsbAndLsb( msPreSend );
                byte delay0 = msp.getKey();
                byte delay1 = msp.getValue();
                add = new VsqNrpn( clock - vsq.getPresendClockAt( clock, msPreSend ), NRPN.CVM_NM_VERSION_AND_DEVICE, (byte)0x00, (byte)0x00 );
                add.append( NRPN.CVM_NM_DELAY, delay0, delay1, true );
                add.append( NRPN.CVM_NM_NOTE_NUMBER, (byte)ve.ID.Note, true ); // Note number
            } else {
                add = new VsqNrpn( clock - vsq.getPresendClockAt( clock, msPreSend ), NRPN.CVM_NM_NOTE_NUMBER, (byte)ve.ID.Note ); // Note number
            }
            add.append( NRPN.CVM_NM_VELOCITY, (byte)ve.ID.Dynamics, true ); // Velocity
            add.append( NRPN.CVM_NM_NOTE_DURATION, duration0, duration1, true ); // Note duration
            add.append( NRPN.CVM_NM_NOTE_LOCATION, note_loc, true ); // Note Location

            if ( ve.ID.VibratoHandle != null ) {
                add.append( NRPN.CVM_NM_INDEX_OF_VIBRATO_DB, (byte)0x00, (byte)0x00, true );
                String icon_id = ve.ID.VibratoHandle.IconID;
                String num = icon_id.Substring( PortUtil.getStringLength( icon_id ) - 4 );
                int vibrato_type = (int)PortUtil.fromHexString( num );
                int note_length = ve.ID.getLength();
                int vibrato_delay = ve.ID.VibratoDelay;
                byte bVibratoDuration = (byte)((float)(note_length - vibrato_delay) / (float)note_length * 127);
                byte bVibratoDelay = (byte)((byte)0x7f - bVibratoDuration);
                add.append( NRPN.CVM_NM_VIBRATO_CONFIG, (byte)vibrato_type, bVibratoDuration, true );
                add.append( NRPN.CVM_NM_VIBRATO_DELAY, bVibratoDelay, true );
            }

            String[] spl = ve.ID.LyricHandle.L0.getPhoneticSymbolList();
            String s = "";
            for ( int j = 0; j < spl.Length; j++ ) {
                s += spl[j];
            }
            char[] symbols = s.ToCharArray();
            if ( renderer.StartsWith( "DSB2" ) ) {
                add.append( 0x5011, (byte)0x01, true );//TODO: (byte)0x5011の意味は解析中
            }
            add.append( NRPN.CVM_NM_PHONETIC_SYMBOL_BYTES, (byte)symbols.Length, true );// (byte)0x12(Number of phonetic symbols in bytes)
            int count = -1;
            for ( int j = 0; j < spl.Length; j++ ) {
                char[] chars = spl[j].ToCharArray();
                for ( int k = 0; k < chars.Length; k++ ) {
                    count++;
                    if ( k == 0 ) {
                        add.append( (0x50 << 8) | (0x13 + count), (byte)chars[k], (byte)ve.ID.LyricHandle.L0.getConsonantAdjustment()[j], true ); // Phonetic symbol j
                    } else {
                        add.append( (0x50 << 8) | (0x13 + count), (byte)chars[k], true ); // Phonetic symbol j
                    }
                }
            }
            if ( !renderer.StartsWith( "DSB2" ) ) {
                add.append( NRPN.CVM_NM_PHONETIC_SYMBOL_CONTINUATION, (byte)0x7f, true ); // End of phonetic symbols
            }
            if ( renderer.StartsWith( "DSB3" ) ) {
                int v1mean = ve.ID.PMBendDepth * 60 / 100;
                if ( v1mean < 0 ) {
                    v1mean = 0;
                }
                if ( 60 < v1mean ) {
                    v1mean = 60;
                }
                int d1mean = (int)(0.3196 * ve.ID.PMBendLength + 8.0);
                int d2mean = (int)(0.92 * ve.ID.PMBendLength + 28.0);
                add.append( NRPN.CVM_NM_V1MEAN, (byte)v1mean, true );// (byte)0x50(v1mean)
                add.append( NRPN.CVM_NM_D1MEAN, (byte)d1mean, true );// (byte)0x51(d1mean)
                add.append( NRPN.CVM_NM_D1MEAN_FIRST_NOTE, (byte)0x14, true );// (byte)0x52(d1meanFirstNote)
                add.append( NRPN.CVM_NM_D2MEAN, (byte)d2mean, true );// (byte)0x53(d2mean)
                add.append( NRPN.CVM_NM_D4MEAN, (byte)ve.ID.d4mean, true );// (byte)0x54(d4mean)
                add.append( NRPN.CVM_NM_PMEAN_ONSET_FIRST_NOTE, (byte)ve.ID.pMeanOnsetFirstNote, true ); // 055(pMeanOnsetFirstNote)
                add.append( NRPN.CVM_NM_VMEAN_NOTE_TRNSITION, (byte)ve.ID.vMeanNoteTransition, true ); // (byte)0x56(vMeanNoteTransition)
                add.append( NRPN.CVM_NM_PMEAN_ENDING_NOTE, (byte)ve.ID.pMeanEndingNote, true );// (byte)0x57(pMeanEndingNote)
                add.append( NRPN.CVM_NM_ADD_PORTAMENTO, (byte)ve.ID.PMbPortamentoUse, true );// (byte)0x58(AddScoopToUpInternals&AddPortamentoToDownIntervals)
                byte decay = (byte)(ve.ID.DEMdecGainRate / 100.0 * (byte)0x64);
                add.append( NRPN.CVM_NM_CHANGE_AFTER_PEAK, decay, true );// (byte)0x59(changeAfterPeak)
                byte accent = (byte)((byte)0x64 * ve.ID.DEMaccent / 100.0);
                add.append( NRPN.CVM_NM_ACCENT, accent, true );// (byte)0x5a(Accent)
            }
            if ( renderer.StartsWith( "UTU0" ) ) {
                // エンベロープ
                if ( ve.UstEvent != null ) {
                    UstEnvelope env = null;
                    if ( ve.UstEvent.Envelope != null ) {
                        env = ve.UstEvent.Envelope;
                    } else {
                        env = new UstEnvelope();
                    }
                    int[] vals = null;
                    vals = new int[10];
                    vals[0] = env.p1;
                    vals[1] = env.p2;
                    vals[2] = env.p3;
                    vals[3] = env.v1;
                    vals[4] = env.v2;
                    vals[5] = env.v3;
                    vals[6] = env.v4;
                    vals[7] = env.p4;
                    vals[8] = env.p5;
                    vals[9] = env.v5;
                    for ( int i = 0; i < vals.Length; i++ ) {
                        //(value3.msb & (byte)0xf) << 28 | (value2.msb & (byte)0x7f) << 21 | (value2.lsb & (byte)0x7f) << 14 | (value1.msb & (byte)0x7f) << 7 | (value1.lsb & (byte)0x7f)
                        byte msb, lsb;
                        int v = vals[i];
                        lsb = (byte)(v & (byte)0x7f);
                        v = v >> 7;
                        msb = (byte)(v & (byte)0x7f);
                        v = v >> 7;
                        add.append( NRPN.CVM_EXNM_ENV_DATA1, msb, lsb );
                        lsb = (byte)(v & (byte)0x7f);
                        v = v >> 7;
                        msb = (byte)(v & (byte)0x7f);
                        v = v >> 7;
                        add.append( NRPN.CVM_EXNM_ENV_DATA2, msb, lsb );
                        msb = (byte)(v & (byte)0xf);
                        add.append( NRPN.CVM_EXNM_ENV_DATA3, msb );
                        add.append( NRPN.CVM_EXNM_ENV_DATA_CONTINUATION, (byte)0x00 );
                    }
                    add.append( NRPN.CVM_EXNM_ENV_DATA_CONTINUATION, (byte)0x7f );

                    // モジュレーション
                    ValuePair<Byte, Byte> m;
                    if ( -100 <= ve.UstEvent.Moduration && ve.UstEvent.Moduration <= 100 ) {
                        m = getMsbAndLsb( ve.UstEvent.Moduration + 100 );
                        add.append( NRPN.CVM_EXNM_MODURATION, m.getKey(), m.getValue() );
                    }

                    // 先行発声
                    if ( ve.UstEvent.PreUtterance != 0 ) {
                        m = getMsbAndLsb( ve.UstEvent.PreUtterance + 8192 );
                        add.append( NRPN.CVM_EXNM_PRE_UTTERANCE, m.getKey(), m.getValue() );
                    }

                    // Flags
                    if ( ve.UstEvent.Flags != "" ) {
                        char[] arr = ve.UstEvent.Flags.ToCharArray();
                        add.append( NRPN.CVM_EXNM_FLAGS_BYTES, (byte)arr.Length );
                        for ( int i = 0; i < arr.Length; i++ ) {
                            byte b = (byte)arr[i];
                            add.append( NRPN.CVM_EXNM_FLAGS, b );
                        }
                        add.append( NRPN.CVM_EXNM_FLAGS_CONINUATION, (byte)0x7f );
                    }

                    // オーバーラップ
                    if ( ve.UstEvent.VoiceOverlap != 0 ) {
                        m = getMsbAndLsb( ve.UstEvent.VoiceOverlap + 8192 );
                        add.append( NRPN.CVM_EXNM_VOICE_OVERLAP, m.getKey(), m.getValue() );
                    }
                }
            }
            add.append( NRPN.CVM_NM_NOTE_MESSAGE_CONTINUATION, (byte)0x7f, true );// (byte)0x7f(Note message continuation)
            return add;
        }

        /// <summary>
        /// 指定したトラックのデータから，NRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <param name="clock_start"></param>
        /// <param name="clock_end"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateNRPN( VsqFile vsq, int track, int msPreSend, int clock_start, int clock_end ) {
            VsqFile temp = (VsqFile)vsq.clone();
            temp.removePart( clock_end, vsq.TotalClocks );
            if ( 0 < clock_start ) {
                temp.removePart( 0, clock_start );
            }
            temp.Master.PreMeasure = 1;
            //temp.m_premeasure_clocks = temp.getClockFromBarCount( 1 );
            VsqNrpn[] ret = generateNRPN( temp, track, msPreSend );
            temp = null;
            return ret;
        }

        /// <summary>
        /// 指定したトラックのデータから，NRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateNRPN( VsqFile vsq, int track, int msPreSend ) {
#if DEBUG
            PortUtil.println( "GenerateNRPN(VsqTrack,int,int,int,int)" );
#endif
            Vector<VsqNrpn> list = new Vector<VsqNrpn>();

            VsqTrack target = vsq.Track.get( track );
            String version = target.getCommon().Version;

            int count = target.getEventCount();
            int note_start = 0;
            int note_end = target.getEventCount() - 1;
            for ( int i = 0; i < count; i++ ) {
                if ( 0 <= target.getEvent( i ).Clock ) {
                    note_start = i;
                    break;
                }
                note_start = i;
            }
            for ( int i = target.getEventCount() - 1; i >= 0; i-- ) {
                if ( target.getEvent( i ).Clock <= vsq.TotalClocks ) {
                    note_end = i;
                    break;
                }
            }

            // 最初の歌手を決める
            int singer_event = -1;
            for ( int i = note_start; i >= 0; i-- ) {
                if ( target.getEvent( i ).ID.type == VsqIDType.Singer ) {
                    singer_event = i;
                    break;
                }
            }
            if ( singer_event >= 0 ) { //見つかった場合
                list.addAll( Arrays.asList( generateSingerNRPN( vsq, target.getEvent( singer_event ), 0 ) ) );
            } else {                   //多分ありえないと思うが、歌手が不明の場合。
                //throw new Exception( "first singer was not specified" );
                list.add( new VsqNrpn( 0, NRPN.CC_BS_LANGUAGE_TYPE, (byte)0x0 ) );
                list.add( new VsqNrpn( 0, NRPN.PC_VOICE_TYPE, (byte)0x0 ) );
            }

            list.addAll( Arrays.asList( generateVoiceChangeParameterNRPN( vsq, track, msPreSend ) ) );
            if ( version.StartsWith( "DSB2" ) ) {
                list.addAll( Arrays.asList( generateFx2DepthNRPN( vsq, track, msPreSend ) ) );
            }

            int ms_presend = msPreSend;
            if ( version.StartsWith( "UTU0" ) ) {
                double sec_maxlen = 0.0;
                for ( Iterator itr = target.getNoteEventIterator(); itr.hasNext(); ) {
                    VsqEvent ve = (VsqEvent)itr.next();
                    double len = vsq.getSecFromClock( ve.Clock + ve.ID.getLength() ) - vsq.getSecFromClock( ve.Clock );
                    sec_maxlen = Math.Max( sec_maxlen, len );
                }
                ms_presend += (int)(sec_maxlen * 1000.0);
            }
            VsqBPList dyn = target.getCurve( "dyn" );
            if ( dyn.size() > 0 ) {
                Vector<VsqNrpn> listdyn = new Vector<VsqNrpn>( Arrays.asList( generateExpressionNRPN( vsq, track, ms_presend ) ) );
                if ( listdyn.size() > 0 ) {
                    list.addAll( listdyn );
                }
            }
            VsqBPList pbs = target.getCurve( "pbs" );
            if ( pbs.size() > 0 ) {
                Vector<VsqNrpn> listpbs = new Vector<VsqNrpn>( Arrays.asList( generatePitchBendSensitivityNRPN( vsq, track, ms_presend ) ) );
                if ( listpbs.size() > 0 ) {
                    list.addAll( listpbs );
                }
            }
            VsqBPList pit = target.getCurve( "pit" );
            if ( pit.size() > 0 ) {
                Vector<VsqNrpn> listpit = new Vector<VsqNrpn>( Arrays.asList( generatePitchBendNRPN( vsq, track, ms_presend ) ) );
                if ( listpit.size() > 0 ) {
                    list.addAll( listpit );
                }
            }

            boolean first = true;
            int last_note_end = 0;
            for ( int i = note_start; i <= note_end; i++ ) {
                VsqEvent item = target.getEvent( i );
                if ( item.ID.type == VsqIDType.Anote ) {
                    byte note_loc = (byte)0x03;
                    if ( item.Clock == last_note_end ) {
                        note_loc -= (byte)0x02;
                    }

                    // 次に現れる音符イベントを探す
                    int nextclock = item.Clock + item.ID.getLength() + 1;
                    int event_count = target.getEventCount();
                    for ( int j = i + 1; j < event_count; j++ ) {
                        VsqEvent itemj = target.getEvent( j );
                        if ( itemj.ID.type == VsqIDType.Anote ) {
                            nextclock = itemj.Clock;
                            break;
                        }
                    }
                    if ( item.Clock + item.ID.getLength() == nextclock ) {
                        note_loc -= (byte)0x01;
                    }

                    list.add( generateNoteNRPN( vsq,
                                                track,
                                                item,
                                                msPreSend,
                                                note_loc,
                                                first ) );
                    first = false;
                    list.addAll( Arrays.asList( generateVibratoNRPN( vsq,
                                                                     item,
                                                                     msPreSend ) ) );
                    last_note_end = item.Clock + item.ID.getLength();
                } else if ( item.ID.type == VsqIDType.Singer ) {
                    if ( i > note_start && i != singer_event ) {
                        list.addAll( Arrays.asList( generateSingerNRPN( vsq, item, msPreSend ) ) );
                    }
                }
            }

            list = VsqNrpn.sort( list );
            Vector<VsqNrpn> merged = new Vector<VsqNrpn>();
            for ( int i = 0; i < list.size(); i++ ) {
                merged.addAll( Arrays.asList( list.get( i ).expand() ) );
            }
            return merged.toArray( new VsqNrpn[] { } );
        }

        /// <summary>
        /// 指定したトラックから、PitchBendのNRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generatePitchBendNRPN( VsqFile vsq, int track, int msPreSend ) {
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();
            VsqBPList pit = vsq.Track.get( track ).getCurve( "PIT" );
            int count = pit.size();
            for ( int i = 0; i < count; i++ ) {
                int clock = pit.getKeyClock( i );
                int value = pit.getElement( i ) + 0x2000;

                ValuePair<Byte, Byte> val = getMsbAndLsb( value );
                int c = clock - vsq.getPresendClockAt( clock, msPreSend );
                if ( c >= 0 ) {
                    VsqNrpn add = new VsqNrpn( c,
                                               NRPN.PB_PITCH_BEND,
                                               val.getKey(),
                                               val.getValue() );
                    ret.add( add );
                }
            }
            return ret.toArray( new VsqNrpn[] { } );
        }

        /// <summary>
        /// 指定したトラックからPitchBendSensitivityのNRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generatePitchBendSensitivityNRPN( VsqFile vsq, int track, int msPreSend ) {
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();
            VsqBPList pbs = vsq.Track.get( track ).getCurve( "PBS" );
            int count = pbs.size();
            for ( int i = 0; i < count; i++ ) {
                int clock = pbs.getKeyClock( i );
                int c = clock - vsq.getPresendClockAt( clock, msPreSend );
                if ( c >= 0 ) {
                    VsqNrpn add = new VsqNrpn( c,
                                               NRPN.CC_PBS_PITCH_BEND_SENSITIVITY,
                                               (byte)pbs.getElement( i ),
                                               (byte)0x00 );
                    ret.add( add );
                }
            }
            return ret.toArray( new VsqNrpn[] { } );
        }

        /// <summary>
        /// 指定した音符イベントから，ビブラート出力用のNRPNを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="ve"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateVibratoNRPN( VsqFile vsq, VsqEvent ve, int msPreSend ) {
            Vector<VsqNrpn> ret = new Vector<VsqNrpn>();
            if ( ve.ID.VibratoHandle != null ) {
                int vclock = ve.Clock + ve.ID.VibratoDelay;
                ValuePair<Byte, Byte> del = getMsbAndLsb( msPreSend );
                VsqNrpn add2 = new VsqNrpn( vclock - vsq.getPresendClockAt( vclock, msPreSend ),
                                            NRPN.CC_VD_VERSION_AND_DEVICE,
                                            (byte)0x00,
                                            (byte)0x00 );
                add2.append( NRPN.CC_VD_DELAY, del.getKey(), del.getValue(), true );
                add2.append( NRPN.CC_VD_VIBRATO_DEPTH, (byte)ve.ID.VibratoHandle.StartDepth, true );
                add2.append( NRPN.CC_VR_VIBRATO_RATE, (byte)ve.ID.VibratoHandle.StartRate );
                ret.add( add2 );
                int vlength = ve.ID.getLength() - ve.ID.VibratoDelay;
                int count = ve.ID.VibratoHandle.RateBP.getCount();
                if ( count > 0 ) {
                    for ( int i = 0; i < count; i++ ) {
                        float percent = ve.ID.VibratoHandle.RateBP.getElement( i ).X;
                        int cl = vclock + (int)(percent * vlength);
                        ret.add( new VsqNrpn( cl - vsq.getPresendClockAt( cl, msPreSend ),
                                              NRPN.CC_VR_VIBRATO_RATE,
                                              (byte)ve.ID.VibratoHandle.RateBP.getElement( i ).Y ) );
                    }
                }
                count = ve.ID.VibratoHandle.DepthBP.getCount();
                if ( count > 0 ) {
                    for ( int i = 0; i < count; i++ ) {
                        float percent = ve.ID.VibratoHandle.DepthBP.getElement( i ).X;
                        int cl = vclock + (int)(percent * vlength);
                        ret.add( new VsqNrpn( cl - vsq.getPresendClockAt( cl, msPreSend ),
                                              NRPN.CC_VD_VIBRATO_DEPTH,
                                              (byte)ve.ID.VibratoHandle.DepthBP.getElement( i ).Y ) );
                    }
                }
            }
            Collections.sort( ret );
            return ret.toArray( new VsqNrpn[] { } );
        }

        /// <summary>
        /// 指定したトラックから、VoiceChangeParameterのNRPNのリストを作成します
        /// </summary>
        /// <param name="vsq"></param>
        /// <param name="track"></param>
        /// <param name="msPreSend"></param>
        /// <returns></returns>
        public static VsqNrpn[] generateVoiceChangeParameterNRPN( VsqFile vsq, int track, int msPreSend ) {
            int premeasure_clock = vsq.getPreMeasureClocks();
            String renderer = vsq.Track.get( track ).getCommon().Version;
            Vector<VsqNrpn> res = new Vector<VsqNrpn>();

            String[] curves;
            if ( renderer.StartsWith( "DSB3" ) ) {
                curves = new String[] { "BRE", "BRI", "CLE", "POR", "OPE", "GEN" };
            } else if ( renderer.StartsWith( "DSB2" ) ) {
                curves = new String[] { "BRE", "BRI", "CLE", "POR", "GEN", "harmonics",
                                        "reso1amp", "reso1bw", "reso1freq", 
                                        "reso2amp", "reso2bw", "reso2freq",
                                        "reso3amp", "reso3bw", "reso3freq",
                                        "reso4amp", "reso4bw", "reso4freq" };
            } else {
                curves = new String[] { "BRE", "BRI", "CLE", "POR", "GEN" };
            }

            for ( int i = 0; i < curves.Length; i++ ) {
                VsqBPList vbpl = vsq.Track.get( track ).getCurve( curves[i] );
                if ( vbpl.size() > 0 ) {
                    byte lsb = NRPN.getVoiceChangeParameterID( curves[i] );
                    int count = vbpl.size();
                    for ( int j = 0; j < count; j++ ) {
                        int clock = vbpl.getKeyClock( j );
                        int c = clock - vsq.getPresendClockAt( clock, msPreSend );
                        if ( c >= 0 ) {
                            VsqNrpn add = new VsqNrpn( c,
                                                       NRPN.VCP_VOICE_CHANGE_PARAMETER_ID,
                                                       lsb );
                            add.append( NRPN.VCP_VOICE_CHANGE_PARAMETER, (byte)vbpl.getElement( j ), true );
                            res.add( add );
                        }
                    }
                }
            }
            Collections.sort( res );
            return res.toArray( new VsqNrpn[] { } );
        }

        public static ValuePair<Byte, Byte> getMsbAndLsb( int value ) {
            ValuePair<Byte, Byte> ret = new ValuePair<Byte, Byte>();
            if ( 0x3fff < value ) {
                ret.setKey( (byte)0x7f );
                ret.setValue( (byte)0x7f );
            } else {
                byte msb = (byte)(value >> 7);
                ret.setKey( msb );
                ret.setValue( (byte)(value - (msb << 7)) );
            }
            return ret;
        }

        public Vector<MidiEvent> generateTimeSig() {
            Vector<MidiEvent> events = new Vector<MidiEvent>();
            for ( Iterator itr = TimesigTable.iterator(); itr.hasNext(); ) {
                TimeSigTableEntry entry = (TimeSigTableEntry)itr.next();
                events.add( MidiEvent.generateTimeSigEvent( entry.Clock, entry.Numerator, entry.Denominator ) );
            }
            return events;
        }

        public Vector<MidiEvent> generateTempoChange() {
            Vector<MidiEvent> events = new Vector<MidiEvent>();
            for ( Iterator itr = TempoTable.iterator(); itr.hasNext(); ) {
                TempoTableEntry entry = (TempoTableEntry)itr.next();
                events.add( MidiEvent.generateTempoChangeEvent( entry.Clock, entry.Tempo ) );
                //last_clock = Math.Max( last_clock, entry.Clock );
            }
            return events;
        }

        /// <summary>
        /// このインスタンスをファイルに出力します
        /// </summary>
        /// <param name="file"></param>
        public void write( String file ) {
            write( file, 500, "Shift_JIS" );
        }

        /// <summary>
        /// このインスタンスをファイルに出力します
        /// </summary>
        /// <param name="file"></param>
        /// <param name="msPreSend">プリセンドタイム(msec)</param>
        public void write( String file, int msPreSend, String encoding ) {
#if DEBUG
            PortUtil.println( "VsqFile.Write(String)" );
#endif
            int last_clock = 0;
            int track_size = Track.size();
            for ( int track = 1; track < track_size; track++ ) {
                if ( Track.get( track ).getEventCount() > 0 ) {
                    int index = Track.get( track ).getEventCount() - 1;
                    VsqEvent last = Track.get( track ).getEvent( index );
                    last_clock = Math.Max( last_clock, last.Clock + last.ID.getLength() );
                }
            }

            if ( PortUtil.isFileExists( file ) ) {
                try {
                    PortUtil.deleteFile( file );
                } catch ( Exception ex ) {
                }
            }

            RandomAccessFile fs = null;
            try {
                fs = new RandomAccessFile( file, "rw" );
                long first_position;//チャンクの先頭のファイル位置

                #region  ヘッダ
                //チャンクタイプ
                fs.write( _MTHD, 0, 4 );
                //データ長
                fs.write( (byte)0x00 );
                fs.write( (byte)0x00 );
                fs.write( (byte)0x00 );
                fs.write( (byte)0x06 );
                //フォーマット
                fs.write( (byte)0x00 );
                fs.write( (byte)0x01 );
                //トラック数
                writeUnsignedShort( fs, Track.size() );
                //時間単位
                fs.write( (byte)0x01 );
                fs.write( (byte)0xe0 );
                #endregion

                #region Master Track
                //チャンクタイプ
                fs.write( _MTRK, 0, 4 );
                //データ長。とりあえず0を入れておく
                fs.write( new byte[] { (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00 }, 0, 4 );
                first_position = fs.getFilePointer();
                //トラック名
                writeFlexibleLengthUnsignedLong( fs, 0 );//デルタタイム
                fs.write( (byte)0xff );//ステータスタイプ
                fs.write( (byte)0x03 );//イベントタイプSequence/Track Name
                fs.write( (byte)_MASTER_TRACK.Length );//トラック名の文字数。これは固定
                fs.write( _MASTER_TRACK, 0, _MASTER_TRACK.Length );

                Vector<MidiEvent> events = new Vector<MidiEvent>();
                for ( Iterator itr = TimesigTable.iterator(); itr.hasNext(); ) {
                    TimeSigTableEntry entry = (TimeSigTableEntry)itr.next();
                    events.add( MidiEvent.generateTimeSigEvent( entry.Clock, entry.Numerator, entry.Denominator ) );
                    last_clock = Math.Max( last_clock, entry.Clock );
                }
                for ( Iterator itr = TempoTable.iterator(); itr.hasNext(); ) {
                    TempoTableEntry entry = (TempoTableEntry)itr.next();
                    events.add( MidiEvent.generateTempoChangeEvent( entry.Clock, entry.Tempo ) );
                    last_clock = Math.Max( last_clock, entry.Clock );
                }
#if DEBUG
                PortUtil.println( "    events.Count=" + events.size() );
#endif
                Collections.sort( events );
                long last = 0;
                for ( Iterator itr = events.iterator(); itr.hasNext(); ) {
                    MidiEvent me = (MidiEvent)itr.next();
#if DEBUG
                    PortUtil.println( "me.Clock=" + me.clock );
#endif
                    writeFlexibleLengthUnsignedLong( fs, (long)(me.clock - last) );
                    me.writeData( fs );
                    last = me.clock;
                }

                //WriteFlexibleLengthUnsignedLong( fs, (ulong)(last_clock + 120 - last) );
                writeFlexibleLengthUnsignedLong( fs, 0 );
                fs.write( (byte)0xff );
                fs.write( (byte)0x2f );//イベントタイプEnd of Track
                fs.write( (byte)0x00 );
                long pos = fs.getFilePointer();
                fs.seek( first_position - 4 );
                writeUnsignedInt( fs, pos - first_position );
                fs.seek( pos );
                #endregion

                #region トラック
                VsqFile temp = (VsqFile)this.clone();
                temp.Track.get( 1 ).setMaster( (VsqMaster)Master.clone() );
                temp.Track.get( 1 ).setMixer( (VsqMixer)Mixer.clone() );
                printTrack( temp, 1, fs, msPreSend, encoding );
                int count = Track.size();
                for ( int track = 2; track < count; track++ ) {
                    printTrack( this, track, fs, msPreSend, encoding );
                }
                #endregion
            } catch ( Exception ex ) {
            } finally {
                if ( fs != null ) {
                    try {
                        fs.close();
                    } catch ( Exception ex2 ) {
                    }
                }
            }
        }

        /// <summary>
        /// メタテキストの行番号から、各行先頭のプレフィクス文字列("DM:0123:"等)を作成します
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static String getLinePrefix( int count ) {
            int digits = getHowManyDigits( count );
            int c = (digits - 1) / 4 + 1;
            String format = "";
            for ( int i = 0; i < c; i++ ) {
                format += "0000";
            }
            return "DM:" + PortUtil.formatDecimal( format, count ) + ":";
        }

        public static byte[] getLinePrefixBytes( int count ) {
            int digits = getHowManyDigits( count );
            int c = (digits - 1) / 4 + 1;
            String format = "";
            for ( int i = 0; i < c; i++ ) {
                format += "0000";
            }
            String str = "DM:" + PortUtil.formatDecimal( format, count ) + ":";
            byte[] ret = PortUtil.getEncodedByte( "ASCII", str );
            return ret;
        }

        /// <summary>
        /// 数numberの桁数を調べます。（10進数のみ）
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private static int getHowManyDigits( int number ) {
            int val;
            if ( number > 0 ) {
                val = number;
            } else {
                val = -number;
            }
            int i = 1;
            int digits = 1;
            while ( true ) {
                i++;
                digits *= 10;
                if ( val < digits ) {
                    return i - 1;
                }
            }
        }

        /// <summary>
        /// char[]を書き込む。
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="item"></param>
        public static void writeCharArray( RandomAccessFile fs, char[] item )
#if JAVA
            throws IOException
#endif
        {
            for ( int i = 0; i < item.Length; i++ ) {
                fs.write( (byte)item[i] );
            }
        }

        /// <summary>
        /// ushort値をビッグエンディアンでfsに書き込みます
        /// </summary>
        /// <param name="data"></param>
        public static void writeUnsignedShort( RandomAccessFile fs, int data )
#if JAVA
            throws IOException
#endif
        {
            byte[] dat = PortUtil.getbytes_uint16_be( data );
            fs.write( dat, 0, dat.Length );
        }

        /// <summary>
        /// uint値をビッグエンディアンでfsに書き込みます
        /// </summary>
        /// <param name="data"></param>
        public static void writeUnsignedInt( RandomAccessFile fs, long data )
#if JAVA
            throws IOException
#endif
        {
            byte[] dat = PortUtil.getbytes_uint32_be( data );
            fs.write( dat, 0, dat.Length );
        }

        /// <summary>
        /// SMFの可変長数値表現を使って、ulongをbyte[]に変換します
        /// </summary>
        /// <param name="number"></param>
        public static byte[] getBytesFlexibleLengthUnsignedLong( long number ) {
            boolean[] bits = new boolean[64];
            long val = (byte)0x1;
            bits[0] = (number & val) == val;
            for ( int i = 1; i < 64; i++ ) {
                val = val << 1;
                bits[i] = (number & val) == val;
            }
            int first = 0;
            for ( int i = 63; i >= 0; i-- ) {
                if ( bits[i] ) {
                    first = i;
                    break;
                }
            }
            // 何バイト必要か？
            int bytes = first / 7 + 1;
            byte[] ret = new byte[bytes];
            for ( int i = 1; i <= bytes; i++ ) {
                int num = 0;
                int count = (byte)0x80;
                for ( int j = (bytes - i + 1) * 7 - 1; j >= (bytes - i + 1) * 7 - 6 - 1; j-- ) {
                    count = count >> 1;
                    if ( bits[j] ) {
                        num += count;
                    }
                }
                if ( i != bytes ) {
                    num += (byte)0x80;
                }
                ret[i - 1] = (byte)num;
            }
            return ret;
        }

        /// <summary>
        /// 整数を書き込む。フォーマットはSMFの可変長数値表現。
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="number"></param>
        public static void writeFlexibleLengthUnsignedLong( RandomAccessFile fs, long number )
#if JAVA
            throws IOException
#endif
        {
            byte[] bytes = getBytesFlexibleLengthUnsignedLong( number );
            fs.write( bytes, 0, bytes.Length );
        }
    }

#if !JAVA
}
#endif
