package org.kbinani.windows.forms;

import java.awt.event.ComponentEvent;
import java.awt.event.ComponentListener;
import javax.swing.JPopupMenu;
import javax.swing.event.PopupMenuEvent;
import javax.swing.event.PopupMenuListener;
import org.kbinani.BEvent;
import org.kbinani.BEventArgs;
import org.kbinani.BEventHandler;
import org.kbinani.componentModel.BCancelEventHandler;

public class BPopupMenu extends JPopupMenu 
                        implements ComponentListener,
                                   PopupMenuListener
{
    private static final long serialVersionUID = 363411779635481115L;
    private Object tag = null;

    public Object getTag(){
        return tag;
    }
    
    public void setTag( Object value ){
        tag = value;
    }

    /* root impl of PopupMenuListener */
    // root impl of PopupMenuListener is in BPopupMenu
    public BEvent<BEventHandler> openingEvent = new BEvent<BEventHandler>();
    public void popupMenuCanceled( PopupMenuEvent e ){
    }
    public void popupMenuWillBecomeInvisible( PopupMenuEvent e ){
    }
    public void popupMenuWillBecomeVisible( PopupMenuEvent e ){
        try{
            openingEvent.raise( this, new BEventArgs() );
        }catch( Exception ex ){
            System.err.println( "BPopupMenu#popupMenuWillBecomeVisible; ex=" + ex );
        }
    }

    // root impl of ComponentListener is in BButton
    public BEvent<BEventHandler> visibleChangedEvent = new BEvent<BEventHandler>();
    public BEvent<BEventHandler> resizeEvent = new BEvent<BEventHandler>();
    public void componentHidden(ComponentEvent e) {
        try{
            visibleChangedEvent.raise( this, new BEventArgs() );
        }catch( Exception ex ){
            System.err.println( "BButton#componentHidden; ex=" + ex );
        }
    }
    public void componentMoved(ComponentEvent e) {
    }
    public void componentResized(ComponentEvent e) {
        try{
            resizeEvent.raise( this, new BEventArgs() );
        }catch( Exception ex ){
            System.err.println( "BButton#componentResized; ex=" + ex );
        }
    }
    public void componentShown(ComponentEvent e) {
        try{
            visibleChangedEvent.raise( this, new BEventArgs() );
        }catch( Exception ex ){
            System.err.println( "BButton#componentShown; ex=" + ex );
        }
    }
}
