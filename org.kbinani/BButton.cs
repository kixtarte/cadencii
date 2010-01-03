/*
 * BButton.cs
 * Copyright (C) 2009-2010 kbinani
 *
 * This file is part of org.kbinani.
 *
 * org.kbinani is free software; you can redistribute it and/or
 * modify it under the terms of the BSD License.
 *
 * org.kbinani is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 */
#if JAVA
//INCLUDE ..\BuildJavaUI\src\org\kbinani\windows\forms\BButton.java
#else
#define COMPONENT_ENABLE_FOCUS
#define COMPONENT_ENABLE_LOCATION
using System;

namespace org.kbinani.windows.forms {

    public class BButton : System.Windows.Forms.Button {
        // root impl of Click event
        #region event impl Click
        // root impl of Click event is in BButton
        public BEvent<BEventHandler> clickEvent = new BEvent<BEventHandler>();
        protected override void OnClick( System.EventArgs e ) {
            base.OnClick( e );
            clickEvent.raise( this, e );
        }
        #endregion

        // root impl of MouseMove event
        #region event impl MouseMove
        // root impl of MouseMove event is in BButton
        public BEvent<BMouseEventHandler> mouseMoveEvent = new BEvent<BMouseEventHandler>();
        protected override void OnMouseMove( System.Windows.Forms.MouseEventArgs mevent ) {
            base.OnMouseMove( mevent );
            mouseMoveEvent.raise( this, mevent );
        }
        #endregion

        // root impl of MouseDown event
        #region event impl MouseDown
        // root impl of MouseDown event is in BButton
        public BEvent<BMouseEventHandler> mouseDownEvent = new BEvent<BMouseEventHandler>();
        protected override void OnMouseDown( System.Windows.Forms.MouseEventArgs mevent ) {
            base.OnMouseDown( mevent );
            mouseDownEvent.raise( this, mevent );
        }
        #endregion

        // root impl of MouseUp event
        #region event impl MouseUp
        // root impl of MouseUp event is in BButton
        public BEvent<BMouseEventHandler> mouseUpEvent = new BEvent<BMouseEventHandler>();
        protected override void OnMouseUp( System.Windows.Forms.MouseEventArgs mevent ) {
            base.OnMouseUp( mevent );
            mouseUpEvent.raise( this, mevent );
        }
        #endregion

        // root impl of MouseEnter event
        #region event impl MouseEnter
        // root impl of MouseEnter event is in BButton
        public BEvent<BEventHandler> mouseEnterEvent = new BEvent<BEventHandler>();
        protected override void OnMouseEnter( System.EventArgs e ) {
            base.OnMouseEnter( e );
            mouseEnterEvent.raise( this, e );
        }
        #endregion

        // root impl of Enter event
        #region event impl Enter
        // root impl of Enter event is in BButton
        public BEvent<BEventHandler> enterEvent = new BEvent<BEventHandler>();
        protected override void OnEnter( System.EventArgs e ) {
            base.OnEnter( e );
            enterEvent.raise( this, e );
        }
        #endregion

        // root impl of MouseLeave event
        #region event impl MouseLeave
        // root impl of MouseLeave event is in BButton
        public BEvent<BEventHandler> mouseLeaveEvent = new BEvent<BEventHandler>();
        protected override void OnMouseLeave( System.EventArgs e ) {
            base.OnMouseLeave( e );
            mouseLeaveEvent.raise( this, e );
        }
        #endregion

        // root impl of MouseDoubleClick event
        #region event impl MouseDoubleClick
        // root impl of MouseDoubleClick is in BButton
        public BEvent<BMouseEventHandler> mouseDoubleClickEvent = new BEvent<BMouseEventHandler>();
        protected override void OnMouseDoubleClick( System.Windows.Forms.MouseEventArgs e ) {
            base.OnMouseDoubleClick( e );
            mouseDoubleClickEvent.raise( this, e );
        }
        #endregion

        // root impl of MouseClick event
        #region event impl MouseClick
        // root impl of MouseClick is in BButton
        public BEvent<BMouseEventHandler> mouseClickEvent = new BEvent<BMouseEventHandler>();
        protected override void OnMouseClick( System.Windows.Forms.MouseEventArgs e ) {
            base.OnMouseClick( e );
            mouseClickEvent.raise( this, e );
        }
        #endregion

        // root impl of Paint event
        #region event impl Paint
        // root impl of Paint is in BButton
        public BEvent<BPaintEventHandler> paintEvent = new BEvent<BPaintEventHandler>();
        protected override void OnPaint( System.Windows.Forms.PaintEventArgs pevent ) {
            base.OnPaint( pevent );
            paintEvent.raise( this, pevent );
        }
        #endregion

        // root impl of Resize event
        #region event impl Resize
        // root impl of Resize event is in BButton
        public BEvent<BEventHandler> resizeEvent = new BEvent<BEventHandler>();
        protected override void OnResize( System.EventArgs e ) {
            base.OnResize( e );
            resizeEvent.raise( this, e );
        }
        #endregion

        // root implf of PreviewKeyDown
        #region event impl PreviewKeyDown
        // root implf of PreviewKeyDown is in BButton
        public BEvent<BPreviewKeyDownEventHandler> previewKeyDownEvent = new BEvent<BPreviewKeyDownEventHandler>();
        protected override void OnPreviewKeyDown( System.Windows.Forms.PreviewKeyDownEventArgs e ) {
            base.OnPreviewKeyDown( e );
            previewKeyDownEvent.raise( this, e );
        }
        #endregion

        // root implf of PreviewKeyDown
        #region event impl LocationChanged
        // root implf of PreviewKeyDown is in BButton
        public BEvent<BEventHandler> locationChangedEvent = new BEvent<BEventHandler>();
        protected override void OnLocationChanged( EventArgs e ) {
            base.OnLocationChanged( e );
            locationChangedEvent.raise( this, e );
        }
        #endregion

        #region java.awt.Component
        // root implementation of java.awt.Component is in BForm.cs
        public java.awt.Dimension getMinimumSize() {
            return new org.kbinani.java.awt.Dimension( base.MinimumSize.Width, base.MinimumSize.Height );
        }

        public void setMinimumSize( java.awt.Dimension value ) {
            base.MinimumSize = new System.Drawing.Size( value.width, value.height );
        }

        public java.awt.Dimension getMaximumSize() {
            return new org.kbinani.java.awt.Dimension( base.MaximumSize.Width, base.MaximumSize.Height );
        }

        public void setMaximumSize( java.awt.Dimension value ) {
            base.MaximumSize = new System.Drawing.Size( value.width, value.height );
        }

        public void invalidate() {
            base.Invalidate();
        }

#if COMPONENT_ENABLE_REPAINT
        public void repaint() {
            base.Refresh();
        }
#endif

#if COMPONENT_ENABLE_CURSOR
        public org.kbinani.java.awt.Cursor getCursor() {
            System.Windows.Forms.Cursor c = base.Cursor;
            org.kbinani.java.awt.Cursor ret = null;
            if( c.Equals( System.Windows.Forms.Cursors.Arrow ) ){
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.DEFAULT_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.Cross ) ){
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.CROSSHAIR_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.Default ) ){
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.DEFAULT_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.Hand ) ){
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.HAND_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.IBeam ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.TEXT_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanEast ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.E_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanNE ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.NE_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanNorth ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.N_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanNW ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.NW_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanSE ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.SE_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanSouth ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.S_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanSW ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.SW_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.PanWest ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.W_RESIZE_CURSOR );
            } else if ( c.Equals( System.Windows.Forms.Cursors.SizeAll ) ) {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.MOVE_CURSOR );
            } else {
                ret = new org.kbinani.java.awt.Cursor( org.kbinani.java.awt.Cursor.CUSTOM_CURSOR );
            }
            ret.cursor = c;
            return ret;
        }

        public void setCursor( org.kbinani.java.awt.Cursor value ) {
            base.Cursor = value.cursor;
        }
#endif

        public bool isVisible() {
            return base.Visible;
        }

        public void setVisible( bool value ) {
            base.Visible = value;
        }

#if COMPONENT_ENABLE_TOOL_TIP_TEXT
        public void setToolTipText( string value )
        {
            base.ToolTipText = value;
        }

        public String getToolTipText()
        {
            return base.ToolTipText;
        }
#endif

#if COMPONENT_PARENT_AS_OWNERITEM
        public Object getParent() {
            return base.OwnerItem;
        }
#else
        public object getParent() {
            return base.Parent;
        }
#endif

        public string getName() {
            return base.Name;
        }

        public void setName( string value ) {
            base.Name = value;
        }

#if COMPONENT_ENABLE_LOCATION
        public void setBounds( int x, int y, int width, int height ) {
            base.Bounds = new System.Drawing.Rectangle( x, y, width, height );
        }

        public void setBounds( org.kbinani.java.awt.Rectangle rc ) {
            base.Bounds = new System.Drawing.Rectangle( rc.x, rc.y, rc.width, rc.height );
        }

        public org.kbinani.java.awt.Point getLocationOnScreen() {
            System.Drawing.Point p = base.PointToScreen( new System.Drawing.Point( 0, 0 ) );
            return new org.kbinani.java.awt.Point( p.X, p.Y );
        }

        public org.kbinani.java.awt.Point getLocation() {
            System.Drawing.Point loc = this.Location;
            return new org.kbinani.java.awt.Point( loc.X, loc.Y );
        }

        public void setLocation( int x, int y ) {
            base.Location = new System.Drawing.Point( x, y );
        }

        public void setLocation( org.kbinani.java.awt.Point p ) {
            base.Location = new System.Drawing.Point( p.x, p.y );
        }
#endif

        public org.kbinani.java.awt.Rectangle getBounds() {
            System.Drawing.Rectangle r = base.Bounds;
            return new org.kbinani.java.awt.Rectangle( r.X, r.Y, r.Width, r.Height );
        }

#if COMPONENT_ENABLE_X
        public int getX() {
            return base.Left;
        }
#endif

#if COMPONENT_ENABLE_Y
        public int getY() {
            return base.Top;
        }
#endif

        public int getWidth() {
            return base.Width;
        }

        public int getHeight() {
            return base.Height;
        }

        public org.kbinani.java.awt.Dimension getSize() {
            return new org.kbinani.java.awt.Dimension( base.Size.Width, base.Size.Height );
        }

        public void setSize( int width, int height ) {
            base.Size = new System.Drawing.Size( width, height );
        }

        public void setSize( org.kbinani.java.awt.Dimension d ) {
            setSize( d.width, d.height );
        }

        public void setBackground( org.kbinani.java.awt.Color color ) {
            base.BackColor = System.Drawing.Color.FromArgb( color.getRed(), color.getGreen(), color.getBlue() );
        }

        public org.kbinani.java.awt.Color getBackground() {
            return new org.kbinani.java.awt.Color( base.BackColor.R, base.BackColor.G, base.BackColor.B );
        }

        public void setForeground( org.kbinani.java.awt.Color color ) {
            base.ForeColor = color.color;
        }

        public org.kbinani.java.awt.Color getForeground() {
            return new org.kbinani.java.awt.Color( base.ForeColor.R, base.ForeColor.G, base.ForeColor.B );
        }

        public bool isEnabled() {
            return base.Enabled;
        }

        public void setEnabled( bool value ) {
            base.Enabled = value;
        }

#if COMPONENT_ENABLE_FOCUS
        public void requestFocus() {
            base.Focus();
        }

        public bool isFocusOwner() {
            return base.Focused;
        }
#endif

        public void setPreferredSize( org.kbinani.java.awt.Dimension size ) {
            base.Size = new System.Drawing.Size( size.width, size.height );
        }

        public org.kbinani.java.awt.Font getFont() {
            return new org.kbinani.java.awt.Font( base.Font );
        }

        public void setFont( org.kbinani.java.awt.Font font ) {
            if ( font == null ) {
                return;
            }
            if ( font.font == null ) {
                return;
            }
            base.Font = font.font;
        }
        #endregion

        #region javax.swing.AbstractButton
        public string getText() {
            return base.Text;
        }

        public void setText( string value ) {
            base.Text = value;
        }
#if ABSTRACT_BUTTON_ENABLE_IS_SELECTED
        public bool isSelected() {
            return base.Checked;
        }

        public void setSelected( bool value ) {
            base.Checked = value;
        }
#endif
        public org.kbinani.java.awt.Icon getIcon() {
            org.kbinani.java.awt.Icon ret = new org.kbinani.java.awt.Icon();
            ret.image = base.Image;
            return ret;
        }

        public void setIcon( org.kbinani.java.awt.Icon value ) {
            base.Image = value.image;
        }
        #endregion

        #region common APIs of org.kbinani.*
        // root implementation is in BForm.cs
        public java.awt.Point pointToScreen( java.awt.Point point_on_client ) {
            java.awt.Point p = getLocationOnScreen();
            return new java.awt.Point( p.x + point_on_client.x, p.y + point_on_client.y );
        }

        public java.awt.Point pointToClient( java.awt.Point point_on_screen ) {
            java.awt.Point p = getLocationOnScreen();
            return new java.awt.Point( point_on_screen.x - p.x, point_on_screen.y - p.y );
        }

#if JAVA
        Object tag = null;
        public Object getTag(){
            return tag;
        }

        public void setTag( Object value ){
            tag = value;
        }
#else
        public Object getTag() {
            return base.Tag;
        }

        public void setTag( Object value ) {
            base.Tag = value;
        }
#endif
        #endregion
    }

}
#endif