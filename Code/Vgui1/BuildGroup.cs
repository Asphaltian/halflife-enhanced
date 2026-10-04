using System.Text;

namespace GoldSrc.Vgui1;

public interface ChangeSignal
{
	void ValueChanged( Panel panel );
}

public class TreeFolder : Panel
{
	private sealed class LabelHandler( TreeFolder treeFolder ) : InputSignal
	{
		public void CursorMoved( int x, int y, Panel panel )
		{
		}

		public void CursorEntered( Panel panel )
		{
		}

		public void CursorExited( Panel panel )
		{
		}

		public void MousePressed( MouseCode code, Panel panel )
		{
			treeFolder.SetOpened( !treeFolder.IsOpened() );
		}

		public void MouseDoublePressed( MouseCode code, Panel panel )
		{
		}

		public void MouseReleased( MouseCode code, Panel panel )
		{
		}

		public void MouseWheeled( int delta, Panel panel )
		{
		}

		public void KeyPressed( KeyCode code, Panel panel )
		{
		}

		public void KeyTyped( KeyCode code, Panel panel )
		{
		}

		public void KeyReleased( KeyCode code, Panel panel )
		{
		}

		public void KeyFocusTicked( Panel panel )
		{
		}
	}

	private sealed class FolderLayout( int hgap, int vgap ) : Layout
	{
		public override void PerformLayout( Panel panel )
		{
			var maxX = 0;
			var y = 0;

			for ( var i = 0; i < panel.GetChildCount(); i++ )
			{
				var child = panel.GetChild( i );

				if ( child is TreeFolder folder )
					folder.InvalidateLayout( true );

				child.GetSize( out var wide, out var tall );

				var x = i == 0 ? 0 : hgap;

				child.SetPos( x, y );

				if ( x + wide > maxX )
					maxX = x + wide;

				y += tall + vgap;
			}

			if ( panel is not TreeFolder treeFolder )
				return;

			if ( treeFolder.IsOpened() )
			{
				panel.SetSize( maxX + 2, y );
				return;
			}

			if ( panel.GetChild( 0 ) is not { } first )
				return;

			first.GetSize( out var firstWide, out var firstTall );
			treeFolder.SetSize( firstWide, firstTall );
		}
	}

	private const int DefaultSize = 500;
	private const int HorizontalGap = 30;
	private const int VerticalGap = 3;
	private const int FolderOffset = 10;
	private const int LineX = 15;

	protected bool _opened;

	public TreeFolder( string name ) : base( 0, 0, DefaultSize, DefaultSize )
	{
		Init( name );
	}

	public TreeFolder( string name, int x, int y ) : base( x, y, DefaultSize, DefaultSize )
	{
		Init( name );
	}

	private void Init( string name )
	{
		_opened = false;

		var label = new Label( name, 0, 0 );

		label.AddInputSignal( new LabelHandler( this ) );
		label.SetParent( this );

		SetLayout( new FolderLayout( HorizontalGap, VerticalGap ) );
	}

	public virtual void SetOpenedTraverse( bool state )
	{
		SetOpened( true );

		for ( var i = 0; i < GetChildCount(); i++ )
		{
			if ( GetChild( i ) is TreeFolder folder )
				folder.SetOpenedTraverse( state );
		}
	}

	public virtual void SetOpened( bool state )
	{
		if ( _opened == state )
			return;

		_opened = state;

		TreeFolder top = null;

		for ( Panel panel = this; panel is not null; panel = panel.GetParent() )
		{
			if ( panel is TreeFolder folder )
			{
				folder.InvalidateLayout( true );
				top = folder;
			}
		}

		top?.RepaintParent();
	}

	public virtual bool IsOpened() => _opened;

	public override void PaintBackground()
	{
		var y = FolderOffset;
		var last = FolderOffset;

		DrawSetColor( SchemeColor.Black );

		for ( var i = 1; i < GetChildCount(); i++ )
		{
			var child = GetChild( i );

			child.GetBounds( out _, out y, out _, out var tall );

			if ( child is TreeFolder )
				y += FolderOffset;
			else
				y += tall / 2;

			DrawFilledRect( LineX, y, HorizontalGap, y + 1 );

			last = y;
		}

		DrawFilledRect( LineX, FolderOffset, LineX + 1, last );

		for ( var i = 1; i < GetChildCount(); i++ )
		{
			var child = GetChild( i );

			child.GetBounds( out _, out y, out _, out _ );

			if ( child is not TreeFolder folder )
				continue;

			y += FolderOffset;

			DrawSetColor( SchemeColor.White );
			DrawFilledRect( 10, y - 5, 21, y + 6 );
			DrawSetColor( SchemeColor.Black );
			DrawOutlinedRect( 10, y - 5, 21, y + 6 );
			DrawFilledRect( 12, y, 19, y + 1 );

			if ( !folder.IsOpened() )
				DrawFilledRect( LineX, y - 3, LineX + 1, y + 4 );
		}
	}
}

public class BuildGroup
{
	private const int DefaultSnap = 4;
	private const int EdgeSize = 4;
	private const string NewLabelName = "Label";

	private bool _enabled;
	private readonly int _snapX;
	private readonly int _snapY;
	private readonly Cursor _cursorSizeNwse;
	private readonly Cursor _cursorSizeNesw;
	private readonly Cursor _cursorSizeWe;
	private readonly Cursor _cursorSizeNs;
	private readonly Cursor _cursorSizeAll;
	private bool _dragging;
	private MouseCode _dragMouseCode;
	private int _dragStartPanelX;
	private int _dragStartPanelY;
	private int _dragStartCursorX;
	private int _dragStartCursorY;
	private Panel _currentPanel;
	private readonly List<ChangeSignal> _currentPanelChangeSignalDar = [];
	private readonly List<Panel> _panelDar = [];
	private readonly List<string> _panelNameDar = [];

	public BuildGroup()
	{
		_enabled = false;
		_snapX = DefaultSnap;
		_snapY = DefaultSnap;
		_cursorSizeNwse = new Cursor( DefaultCursor.SizeNwse );
		_cursorSizeNesw = new Cursor( DefaultCursor.SizeNesw );
		_cursorSizeWe = new Cursor( DefaultCursor.SizeWe );
		_cursorSizeNs = new Cursor( DefaultCursor.SizeNs );
		_cursorSizeAll = new Cursor( DefaultCursor.SizeAll );
		_currentPanel = null;
		_dragging = false;
	}

	public virtual void SetEnabled( bool state )
	{
		if ( _enabled != state )
		{
			if ( _currentPanel is not null )
			{
				_currentPanel = null;

				FireCurrentPanelChangeSignal();
			}

			_currentPanel = null;
		}

		_enabled = state;
	}

	public virtual bool IsEnabled() => _enabled;

	public virtual void AddCurrentPanelChangeSignal( ChangeSignal s )
	{
		if ( !_currentPanelChangeSignalDar.Contains( s ) )
			_currentPanelChangeSignalDar.Add( s );
	}

	public virtual Panel GetCurrentPanel() => _currentPanel;

	public virtual void CopyPropertiesToClipboard()
	{
		var text = new StringBuilder();

		for ( var i = 0; i < _panelDar.Count; i++ )
		{
			text.Append( i < _panelNameDar.Count ? _panelNameDar[i] : null );
			text.Append( _panelDar[i].GetPersistanceText() );
		}

		App.GetInstance().SetClipboardText( text.ToString(), text.Length );

		Log.Info( "Copied to clipboard\n" );
	}

	protected virtual void ApplySnap( Panel panel )
	{
		panel.GetBounds( out var x, out var y, out var wide, out var tall );

		x = x / _snapX * _snapX;
		y = y / _snapY * _snapY;

		panel.SetPos( x, y );
		panel.SetSize( ((x + wide) / _snapX * _snapX) - x, ((y + tall) / _snapY * _snapY) - y );
	}

	protected virtual void FireCurrentPanelChangeSignal()
	{
		for ( var i = 0; i < _currentPanelChangeSignalDar.Count; i++ )
			_currentPanelChangeSignalDar[i].ValueChanged( null );
	}

	public virtual void PanelAdded( Panel panel, string panelName )
	{
		_panelDar.Add( panel );
		_panelNameDar.Add( panelName );
	}

	public virtual void CursorMoved( int x, int y, Panel panel )
	{
		if ( !_dragging )
			return;

		panel.GetApp().GetCursorPos( out x, out y );

		if ( _dragMouseCode == MouseCode.Right )
			panel.SetSize( x - _dragStartCursorX, y - _dragStartCursorY );
		else
			panel.SetPos( _dragStartPanelX - _dragStartCursorX + x, _dragStartPanelY - _dragStartCursorY + y );

		ApplySnap( panel );

		panel.RepaintParent();
	}

	public virtual void MousePressed( MouseCode code, Panel panel )
	{
		if ( code == MouseCode.Right )
		{
			panel.GetApp().GetCursorPos( out var x, out var y );
			panel.ScreenToLocal( ref x, ref y );

			var label = new Label( NewLabelName, x, y, 0, 0 );

			label.SetBorder( new LineBorder() );
			label.SetParent( panel );
			label.SetBuildGroup( this, NewLabelName );

			panel = label;
		}

		_dragging = true;
		_dragMouseCode = code;

		panel.RequestFocus();
		panel.GetApp().GetCursorPos( out _dragStartCursorX, out _dragStartCursorY );
		panel.GetPos( out _dragStartPanelX, out _dragStartPanelY );
		panel.GetApp().SetMouseCapture( panel );

		if ( _currentPanel == panel )
			return;

		_currentPanel = panel;

		FireCurrentPanelChangeSignal();
	}

	public virtual void MouseReleased( MouseCode code, Panel panel )
	{
		_dragging = false;

		panel.GetApp().SetMouseCapture( null );
	}

	public virtual void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public virtual void KeyTyped( KeyCode code, Panel panel )
	{
		var dx = 0;
		var dy = 0;

		var shift = panel.IsKeyDown( KeyCode.LShift ) || panel.IsKeyDown( KeyCode.RShift );
		var ctrl = panel.IsKeyDown( KeyCode.LControl ) || panel.IsKeyDown( KeyCode.RControl );

		switch ( code )
		{
			case KeyCode.C:
				if ( ctrl )
					CopyPropertiesToClipboard();

				return;
			case KeyCode.Up:
				dy = -_snapY;
				break;
			case KeyCode.Left:
				dx = -_snapX;
				break;
			case KeyCode.Down:
				dy = _snapY;
				break;
			case KeyCode.Right:
				dx = _snapX;
				break;
			default:
				return;
		}

		if ( dx == 0 && dy == 0 )
			return;

		panel.GetBounds( out var x, out var y, out var wide, out var tall );

		if ( shift )
			panel.SetSize( wide + dx, tall + dy );
		else
			panel.SetPos( x + dx, y + dy );

		ApplySnap( panel );

		panel.Repaint();
		panel.GetParent()?.Repaint();
	}

	public virtual Cursor GetCursor( Panel panel )
	{
		panel.GetApp().GetCursorPos( out var x, out var y );
		panel.ScreenToLocal( ref x, ref y );
		panel.GetSize( out _, out var tall );

		if ( x >= 2 )
			return _cursorSizeAll;

		if ( y < EdgeSize )
			return _cursorSizeNwse;

		if ( y < tall - EdgeSize )
			return _cursorSizeWe;

		return _cursorSizeNesw;
	}
}
