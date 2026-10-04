namespace GoldSrc.Vgui1;

public interface ActionSignal
{
	void ActionPerformed( Panel panel );
}

public interface ButtonController
{
	void AddSignals( Button button );
	void RemoveSignals( Button button );
}

public class ButtonGroup
{
	protected readonly List<Button> _buttonDar = [];

	public virtual void AddButton( Button button )
	{
		if ( !_buttonDar.Contains( button ) )
			_buttonDar.Add( button );
	}

	public virtual void SetSelected( Button button )
	{
		for ( var i = 0; i < _buttonDar.Count; i++ )
		{
			if ( _buttonDar[i] != button )
				_buttonDar[i].SetSelectedDirect( false );
		}

		button.SetSelectedDirect( true );
	}
}

public class Button : Label
{
	private sealed class DefaultButtonController( Button button ) : ButtonController, InputSignal
	{
		public void AddSignals( Button target ) => target.AddInputSignal( this );

		public void RemoveSignals( Button target ) => target.RemoveInputSignal( this );

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
			if ( !button.IsEnabled() || !button.IsMouseClickEnabled( code ) )
				return;

			button.SetSelected( true );
			button.Repaint();
		}

		public void MouseDoublePressed( MouseCode code, Panel panel )
		{
		}

		public void MouseReleased( MouseCode code, Panel panel )
		{
			if ( !button.IsEnabled() || !button.IsMouseClickEnabled( code ) )
				return;

			button.SetSelected( false );
			button.FireActionSignal();
			button.Repaint();
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

	protected bool _armed;
	protected bool _selected;
	protected bool _buttonBorderEnabled;
	protected readonly List<ActionSignal> _actionSignalDar = [];
	protected int _mouseClickMask;
	protected ButtonGroup _buttonGroup;
	protected ButtonController _buttonController;

	public Button( string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		Init();
	}

	public Button( string text, int x, int y ) : base( text, x, y )
	{
		Init();
	}

	private void Init()
	{
		_armed = false;
		_selected = false;
		_buttonBorderEnabled = true;
		_mouseClickMask = 0;
		_buttonController = null;
		_buttonGroup = null;

		_mouseClickMask |= 1 << ((int)MouseCode.Left + 1);

		_buttonController = new DefaultButtonController( this );
		_buttonController.AddSignals( this );
	}

	public virtual void SetSelected( bool state )
	{
		if ( _buttonGroup is not null )
		{
			_buttonGroup.SetSelected( this );
			return;
		}

		SetSelectedDirect( state );
	}

	public virtual void SetSelectedDirect( bool state )
	{
		_selected = state;

		Repaint();
	}

	public virtual void SetArmed( bool state )
	{
		_armed = state;

		Repaint();
	}

	public virtual bool IsSelected() => _selected;

	public virtual void DoClick()
	{
		SetSelected( true );
		FireActionSignal();
		SetSelected( false );
	}

	public virtual void AddActionSignal( ActionSignal s )
	{
		if ( s is not null && !_actionSignalDar.Contains( s ) )
			_actionSignalDar.Add( s );
	}

	public virtual void SetButtonGroup( ButtonGroup buttonGroup )
	{
		_buttonGroup = buttonGroup;

		buttonGroup?.AddButton( this );
	}

	public virtual bool IsArmed() => _armed;

	public virtual void SetButtonBorderEnabled( bool state )
	{
		_buttonBorderEnabled = state;

		Repaint();
	}

	public virtual void SetMouseClickEnabled( MouseCode code, bool state )
	{
		if ( state )
			_mouseClickMask |= 1 << ((int)code + 1);
		else
			_mouseClickMask &= ~(1 << ((int)code + 1));
	}

	public virtual bool IsMouseClickEnabled( MouseCode code ) => (_mouseClickMask & (1 << ((int)code + 1))) != 0;

	public virtual void FireActionSignal()
	{
		for ( var i = 0; i < _actionSignalDar.Count; i++ )
			_actionSignalDar[i].ActionPerformed( this );
	}

	public override Panel CreatePropertyPanel()
	{
		var parentPanel = base.CreatePropertyPanel();
		var folder = new TreeFolder( "Button" );

		parentPanel.AddChild( folder );

		folder.AddChild( new Label( "setSelected" ) );
		folder.AddChild( new Label( "setArmed" ) );

		return parentPanel;
	}

	protected virtual void SetButtonController( ButtonController buttonController )
	{
		_buttonController?.RemoveSignals( this );
		_buttonController = buttonController;

		buttonController.AddSignals( this );
	}

	public override void PaintBackground()
	{
		GetPaintSize( out var wide, out var tall );

		if ( IsSelected() )
		{
			DrawSetColor( SchemeColor.Secondary2 );
			DrawFilledRect( 0, 0, wide, tall );
		}
		else
		{
			DrawSetColor( SchemeColor.Secondary3 );
			DrawFilledRect( 0, 0, wide, tall );

			if ( _buttonBorderEnabled )
			{
				DrawSetColor( SchemeColor.Secondary1 );
				DrawFilledRect( 0, 0, _wide - 1, 1 );
				DrawFilledRect( 2, _tall - 2, _wide - 1, _tall - 1 );
				DrawFilledRect( 0, 1, 1, _tall - 1 );
				DrawFilledRect( _wide - 2, 2, _wide - 1, _tall - 2 );

				DrawSetColor( SchemeColor.White );
				DrawFilledRect( 1, 1, _wide - 2, 2 );
				DrawFilledRect( 1, _tall - 1, _wide, _tall );
				DrawFilledRect( 1, 2, 2, _tall - 2 );
				DrawFilledRect( _wide - 1, 1, _wide, _tall - 1 );
			}
		}

		if ( !IsArmed() )
			return;

		DrawSetColor( SchemeColor.White );
		DrawFilledRect( 0, 0, wide, 2 );
		DrawFilledRect( 0, 2, 2, tall );

		DrawSetColor( SchemeColor.Secondary2 );
		DrawFilledRect( 2, tall - 2, wide, tall );
		DrawFilledRect( wide - 2, 2, wide, tall - 1 );

		DrawSetColor( SchemeColor.Secondary1 );
		DrawFilledRect( 1, tall - 1, wide, tall );
		DrawFilledRect( wide - 1, 1, wide, tall - 1 );
	}
}
