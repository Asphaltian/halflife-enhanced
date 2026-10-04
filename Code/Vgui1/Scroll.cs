namespace GoldSrc.Vgui1;

public interface IntChangeSignal
{
	void IntChanged( int value, Panel panel );
}

public class Slider : Panel
{
	private sealed class Handler( Slider slider ) : InputSignal
	{
		public void CursorMoved( int x, int y, Panel panel ) => slider.PrivateCursorMoved( x, y, panel );

		public void CursorEntered( Panel panel )
		{
		}

		public void CursorExited( Panel panel )
		{
		}

		public void MousePressed( MouseCode code, Panel panel ) => slider.PrivateMousePressed( code, panel );

		public void MouseDoublePressed( MouseCode code, Panel panel )
		{
		}

		public void MouseReleased( MouseCode code, Panel panel ) => slider.PrivateMouseReleased( code, panel );

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

	private const int DefaultRangeMax = 299;

	private readonly bool _vertical;
	private bool _dragging;
	private int _nobPos0;
	private int _nobPos1;
	private int _nobDragStartPos0;
	private int _nobDragStartPos1;
	private int _dragStartPosX;
	private int _dragStartPosY;
	private readonly List<IntChangeSignal> _intChangeSignalDar = [];
	private int _rangeMin;
	private int _rangeMax;
	private int _value;
	private int _rangeWindow;
	private bool _rangeWindowEnabled;
	private int _buttonOffset;

	public Slider( int x, int y, int wide, int tall, bool vertical ) : base( x, y, wide, tall )
	{
		_vertical = vertical;
		_dragging = false;
		_value = 0;
		_rangeMin = 0;
		_rangeMax = DefaultRangeMax;
		_rangeWindow = 0;
		_rangeWindowEnabled = false;
		_buttonOffset = 0;

		RecomputeNobPosFromValue();
		AddInputSignal( new Handler( this ) );
	}

	public virtual void SetValue( int value )
	{
		var oldValue = _value;

		if ( value < _rangeMin )
			value = _rangeMin;

		if ( value > _rangeMax )
			value = _rangeMax;

		_value = value;

		RecomputeNobPosFromValue();

		if ( _value != oldValue )
			FireIntChangeSignal();
	}

	public virtual int GetValue() => _value;

	public virtual bool IsVertical() => _vertical;

	public virtual void AddIntChangeSignal( IntChangeSignal s )
	{
		if ( !_intChangeSignalDar.Contains( s ) )
			_intChangeSignalDar.Add( s );
	}

	public virtual void SetRange( int min, int max )
	{
		_rangeMin = min;
		_rangeMax = max < min ? min : max;
	}

	public virtual void GetRange( out int min, out int max )
	{
		min = _rangeMin;
		max = _rangeMax;
	}

	public virtual void SetRangeWindow( int rangeWindow )
	{
		_rangeWindow = rangeWindow;
	}

	public virtual void SetRangeWindowEnabled( bool state )
	{
		_rangeWindowEnabled = state;
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		RecomputeNobPosFromValue();
	}

	public virtual void GetNobPos( out int min, out int max )
	{
		min = _nobPos0;
		max = _nobPos1;
	}

	private float RangeWindow()
	{
		float rangeWindow = _rangeWindow;

		if ( rangeWindow < 0f )
			rangeWindow = 0f;

		if ( !_rangeWindowEnabled )
			rangeWindow = _rangeMax - _rangeMin;

		return rangeWindow;
	}

	public virtual bool HasFullRange()
	{
		GetPaintSize( out var wide, out var tall );

		var rangeWindow = RangeWindow();

		if ( rangeWindow > 0f )
			return rangeWindow <= (double)_buttonOffset + (_vertical ? (float)tall : (float)wide);

		return false;
	}

	public virtual void SetButtonOffset( int buttonOffset )
	{
		_buttonOffset = buttonOffset;
	}

	protected virtual void RecomputeNobPosFromValue()
	{
		GetPaintSize( out var wide, out var tall );

		float range = _rangeMax - _rangeMin;
		float value = _value - _rangeMin;
		var per = (float)((double)value / range);
		var rangeWindow = RangeWindow();

		if ( rangeWindow > 0f )
		{
			var size = _vertical ? tall : wide;
			float fsize = size;
			var nobSize = (double)fsize / rangeWindow * fsize;
			var position = (fsize - nobSize) * per;

			_nobPos0 = (int)position;
			_nobPos1 = (int)(position + nobSize);

			if ( _nobPos1 > size )
			{
				_nobPos0 = size - (int)nobSize;
				_nobPos1 = size;
			}
		}

		Repaint();
	}

	protected virtual void RecomputeValueFromNobPos()
	{
		GetPaintSize( out var wide, out var tall );

		float range = _rangeMax - _rangeMin;
		float value = _value - _rangeMin;
		float nobPos = _nobPos0;
		var rangeWindow = RangeWindow();
		double offset = value;

		if ( rangeWindow > 0f )
		{
			float fsize = _vertical ? tall : wide;
			var free = fsize - ((double)fsize / rangeWindow * fsize);

			offset = nobPos / free * range;
		}

		var result = (int)(_rangeMin + offset + 0.0);

		_value = result >= _rangeMax ? _rangeMax : result;
	}

	public virtual void PrivateCursorMoved( int x, int y, Panel panel )
	{
		if ( !_dragging )
			return;

		GetApp().GetCursorPos( out x, out y );
		ScreenToLocal( ref x, ref y );
		GetPaintSize( out var wide, out var tall );

		int limit;

		if ( _vertical )
		{
			_nobPos0 = _nobDragStartPos0 - _dragStartPosY + y;
			_nobPos1 = _nobDragStartPos1 - _dragStartPosY + y;

			limit = tall;
		}
		else
		{
			_nobPos0 = _nobDragStartPos0 - _dragStartPosX + x;
			_nobPos1 = _nobDragStartPos1 - _dragStartPosX + x;

			limit = wide;
		}

		if ( _nobPos1 > limit )
		{
			_nobPos0 = limit + (_nobPos0 - _nobPos1);
			_nobPos1 = limit;
		}

		if ( _nobPos0 < 0 )
		{
			_nobPos1 -= _nobPos0;
			_nobPos0 = 0;
		}

		RecomputeValueFromNobPos();
		Repaint();
		FireIntChangeSignal();
	}

	public virtual void PrivateMousePressed( MouseCode code, Panel panel )
	{
		GetApp().GetCursorPos( out var x, out var y );
		ScreenToLocal( ref x, ref y );

		var position = _vertical ? y : x;

		if ( position < _nobPos0 || position >= _nobPos1 )
			return;

		_dragging = true;

		GetApp().SetMouseCapture( this );

		_nobDragStartPos0 = _nobPos0;
		_nobDragStartPos1 = _nobPos1;
		_dragStartPosX = x;
		_dragStartPosY = y;
	}

	public virtual void PrivateMouseReleased( MouseCode code, Panel panel )
	{
		_dragging = false;

		GetApp().SetMouseCapture( null );
	}

	protected virtual void FireIntChangeSignal()
	{
		for ( var i = 0; i < _intChangeSignalDar.Count; i++ )
			_intChangeSignalDar[i].IntChanged( GetValue(), this );
	}

	public override void PaintBackground()
	{
		GetPaintSize( out var wide, out var tall );

		if ( !_vertical )
		{
			DrawSetColor( SchemeColor.Secondary3 );
			DrawFilledRect( 0, 0, wide, tall );

			DrawSetColor( SchemeColor.Black );
			DrawOutlinedRect( 0, 0, wide, tall );

			DrawSetColor( SchemeColor.Primary2 );
			DrawFilledRect( _nobPos0, 0, _nobPos1, tall );

			DrawSetColor( SchemeColor.Black );
			DrawOutlinedRect( _nobPos0, 0, _nobPos1, tall );

			return;
		}

		DrawSetColor( SchemeColor.Secondary1 );
		DrawFilledRect( 0, 0, wide, 1 );
		DrawFilledRect( 0, tall - 1, wide, tall );
		DrawFilledRect( 0, 1, 1, tall - 1 );
		DrawFilledRect( wide - 1, 1, wide, tall - 1 );

		DrawSetColor( SchemeColor.Secondary2 );
		DrawFilledRect( 1, 1, wide - 1, 2 );
		DrawFilledRect( 1, 2, 3, tall - 1 );
		DrawFilledRect( 2, _nobPos1, wide - 1, _nobPos1 + 1 );

		DrawSetColor( SchemeColor.Secondary3 );
		DrawFilledRect( 2, 2, wide - 1, tall - 1 );

		DrawSetColor( SchemeColor.Primary1 );
		DrawFilledRect( 0, _nobPos0, wide, _nobPos0 + 1 );
		DrawFilledRect( 0, _nobPos1, wide, _nobPos1 + 1 );
		DrawFilledRect( 0, _nobPos0 + 1, 1, _nobPos1 );
		DrawFilledRect( wide - 1, _nobPos0 + 1, wide, _nobPos1 );

		DrawSetColor( SchemeColor.Primary3 );
		DrawFilledRect( 1, _nobPos0 + 1, wide - 1, _nobPos0 + 2 );
		DrawFilledRect( 1, _nobPos0 + 2, 2, _nobPos1 );

		DrawSetColor( SchemeColor.Primary2 );
		DrawFilledRect( 2, _nobPos0 + 2, wide - 1, _nobPos1 );
	}
}

public class ScrollBar : Panel
{
	private sealed class SliderHandler( ScrollBar scrollBar ) : IntChangeSignal
	{
		public void IntChanged( int value, Panel panel ) => scrollBar.FireIntChangeSignal();
	}

	private sealed class ButtonHandler( ScrollBar scrollBar, int buttonIndex ) : ActionSignal
	{
		public void ActionPerformed( Panel panel ) => scrollBar.DoButtonPressed( buttonIndex );
	}

	private const int DefaultButtonPressedScrollValue = 15;

	protected readonly Button[] _button = new Button[2];
	protected Slider _slider;
	protected readonly List<IntChangeSignal> _intChangeSignalDar = [];
	protected int _buttonPressedScrollValue;

	public ScrollBar( int x, int y, int wide, int tall, bool vertical ) : base( x, y, wide, tall )
	{
		_slider = null;
		_button[0] = null;
		_button[1] = null;

		if ( vertical )
		{
			SetSlider( new Slider( 0, wide - 1, wide, tall - (wide * 2) + 2, true ) );
			SetButton( new Button( "", 0, 0, wide, wide ), 0 );
			SetButton( new Button( "", 0, tall - wide, wide, wide ), 1 );
		}
		else
		{
			SetSlider( new Slider( tall, 0, wide - (tall * 2), tall, false ) );
			SetButton( new Button( "", 0, 0, tall + 1, tall + 1 ), 0 );
			SetButton( new Button( "", wide - tall, 0, tall + 1, tall + 1 ), 1 );
		}

		SetPaintBorderEnabled( true );
		SetPaintBackgroundEnabled( true );
		SetPaintEnabled( true );
		SetButtonPressedScrollValue( DefaultButtonPressedScrollValue );
		Validate();
	}

	public virtual void SetValue( int value ) => _slider.SetValue( value );

	public virtual int GetValue() => _slider.GetValue();

	public virtual void AddIntChangeSignal( IntChangeSignal s )
	{
		if ( !_intChangeSignalDar.Contains( s ) )
			_intChangeSignalDar.Add( s );

		_slider.AddIntChangeSignal( new SliderHandler( this ) );
	}

	public virtual void SetRange( int min, int max ) => _slider.SetRange( min, max );

	public virtual void SetRangeWindow( int rangeWindow ) => _slider.SetRangeWindow( rangeWindow );

	public virtual void SetRangeWindowEnabled( bool state ) => _slider.SetRangeWindowEnabled( state );

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		if ( _slider is null || _button[0] is null || _button[1] is null )
			return;

		GetPaintSize( out wide, out tall );

		if ( _slider.IsVertical() )
		{
			_slider.SetBounds( 0, wide, wide, tall - (wide * 2) );
			_button[0].SetBounds( 0, 0, wide, wide );
			_button[1].SetBounds( 0, tall - wide, wide, wide );
		}
		else
		{
			_slider.SetBounds( tall, 0, wide - (tall * 2), tall );
			_button[0].SetBounds( 0, 0, tall, tall );
			_button[1].SetBounds( wide - tall, 0, tall, tall );
		}
	}

	public virtual bool IsVertical() => _slider.IsVertical();

	public virtual bool HasFullRange() => _slider.HasFullRange();

	public virtual void SetButton( Button button, int index )
	{
		if ( _button[index] is not null )
			RemoveChild( _button[index] );

		_button[index] = button;

		AddChild( button );

		_button[index].AddActionSignal( new ButtonHandler( this, index ) );

		Validate();
	}

	public virtual Button GetButton( int index ) => _button[index];

	public virtual void SetSlider( Slider slider )
	{
		if ( _slider is not null )
			RemoveChild( _slider );

		_slider = slider;

		AddChild( slider );

		_slider.AddIntChangeSignal( new SliderHandler( this ) );

		Validate();
	}

	public virtual Slider GetSlider() => _slider;

	public virtual void DoButtonPressed( int buttonIndex )
	{
		if ( buttonIndex == 0 )
			_slider.SetValue( _slider.GetValue() - _buttonPressedScrollValue );
		else
			_slider.SetValue( _slider.GetValue() + _buttonPressedScrollValue );
	}

	public virtual void SetButtonPressedScrollValue( int value )
	{
		_buttonPressedScrollValue = value;
	}

	public virtual void Validate()
	{
		if ( _slider is not null )
		{
			var buttonOffset = 0;

			for ( var i = 0; i < 2; i++ )
			{
				if ( _button[i] is null || !_button[i].IsVisible() )
					continue;

				buttonOffset += _slider.IsVertical() ? _button[i].GetTall() : _button[i].GetWide();
			}

			_slider.SetButtonOffset( buttonOffset );
		}

		GetSize( out var wide, out var tall );
		SetSize( wide, tall );
	}

	public virtual void FireIntChangeSignal()
	{
		for ( var i = 0; i < _intChangeSignalDar.Count; i++ )
			_intChangeSignalDar[i].IntChanged( _slider.GetValue(), this );
	}

	public override void PerformLayout()
	{
	}
}

public class ScrollPanel : Panel
{
	private sealed class ScrollHandler( ScrollPanel scrollPanel ) : IntChangeSignal
	{
		public void IntChanged( int value, Panel panel ) => scrollPanel.RecomputeScroll();
	}

	private const int ScrollBarSize = 16;
	private const int ValidatePasses = 4;

	private readonly Panel _clientClip;
	private readonly Panel _client;
	private readonly ScrollBar _horizontalScrollBar;
	private readonly ScrollBar _verticalScrollBar;
	private bool _autoVisibleHorizontal;
	private bool _autoVisibleVertical;

	public ScrollPanel( int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		SetPaintBorderEnabled( true );
		SetPaintBackgroundEnabled( false );
		SetPaintEnabled( false );

		_clientClip = new Panel( 0, 0, wide - ScrollBarSize, tall - ScrollBarSize );
		_clientClip.SetParent( this );
		_clientClip.SetBgColor( new Color( 0, 128, 0, 0 ) );
		_clientClip.SetPaintBorderEnabled( true );
		_clientClip.SetPaintBackgroundEnabled( false );
		_clientClip.SetPaintEnabled( false );

		_client = new Panel( 0, 0, wide * 2, tall * 2 );
		_client.SetParent( _clientClip );
		_client.SetPaintBorderEnabled( true );
		_client.SetPaintBackgroundEnabled( false );
		_client.SetPaintEnabled( false );

		_horizontalScrollBar = new ScrollBar( 0, tall - ScrollBarSize, wide - ScrollBarSize, ScrollBarSize, false );
		_horizontalScrollBar.SetParent( this );
		_horizontalScrollBar.AddIntChangeSignal( new ScrollHandler( this ) );
		_horizontalScrollBar.SetVisible( false );

		_verticalScrollBar = new ScrollBar( wide - ScrollBarSize, 0, ScrollBarSize, tall - ScrollBarSize, true );
		_verticalScrollBar.SetParent( this );
		_verticalScrollBar.AddIntChangeSignal( new ScrollHandler( this ) );
		_verticalScrollBar.SetVisible( false );

		_autoVisibleHorizontal = true;
		_autoVisibleVertical = true;

		Validate();
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		GetPaintSize( out wide, out tall );

		if ( _autoVisibleHorizontal )
			_horizontalScrollBar.SetVisible( !_horizontalScrollBar.HasFullRange() );

		if ( _autoVisibleVertical )
			_verticalScrollBar.SetVisible( !_verticalScrollBar.HasFullRange() );

		if ( _verticalScrollBar.IsVisible() )
			wide -= _verticalScrollBar.GetWide();

		if ( _horizontalScrollBar.IsVisible() )
			tall -= _horizontalScrollBar.GetTall();

		_verticalScrollBar.SetBounds( wide, 0, _verticalScrollBar.GetWide(), tall );
		_horizontalScrollBar.SetBounds( 0, tall, wide, _horizontalScrollBar.GetTall() );
		_clientClip.SetSize( wide, tall );

		RecomputeClientSize();
		Repaint();
	}

	public virtual void SetScrollBarVisible( bool horizontal, bool vertical )
	{
		_horizontalScrollBar.SetVisible( horizontal );
		_verticalScrollBar.SetVisible( vertical );

		Validate();
	}

	public virtual void SetScrollBarAutoVisible( bool horizontal, bool vertical )
	{
		_autoVisibleHorizontal = horizontal;
		_autoVisibleVertical = vertical;

		Validate();
	}

	public virtual Panel GetClient() => _client;

	public virtual Panel GetClientClip() => _clientClip;

	public virtual void SetScrollValue( int horizontal, int vertical )
	{
		_horizontalScrollBar.SetValue( horizontal );
		_verticalScrollBar.SetValue( vertical );

		RecomputeScroll();
	}

	public virtual void GetScrollValue( out int horizontal, out int vertical )
	{
		horizontal = _horizontalScrollBar.GetValue();
		vertical = _verticalScrollBar.GetValue();
	}

	public virtual void RecomputeClientSize()
	{
		var maxWide = 0;
		var maxTall = 0;

		for ( var i = 0; i < _client.GetChildCount(); i++ )
		{
			var child = _client.GetChild( i );

			if ( !child.IsVisible() )
				continue;

			child.GetPos( out var x, out var y );
			child.GetVirtualSize( out var wide, out var tall );

			x += wide;
			y += tall;

			if ( x > maxWide )
				maxWide = x;

			if ( y > maxTall )
				maxTall = y;
		}

		_client.SetSize( maxWide, maxTall );

		_horizontalScrollBar.SetRange( 0, _client.GetWide() - _clientClip.GetWide() );
		_horizontalScrollBar.SetRangeWindow( _client.GetWide() );

		_verticalScrollBar.SetRange( 0, _client.GetTall() - _clientClip.GetTall() );
		_verticalScrollBar.SetRangeWindow( _client.GetTall() );
	}

	public virtual ScrollBar GetHorizontalScrollBar() => _horizontalScrollBar;

	public virtual ScrollBar GetVerticalScrollBar() => _verticalScrollBar;

	public virtual void Validate()
	{
		_horizontalScrollBar.SetRangeWindowEnabled( true );
		_verticalScrollBar.SetRangeWindowEnabled( true );

		GetSize( out var wide, out var tall );

		for ( var i = 0; i < ValidatePasses; i++ )
			SetSize( wide, tall );
	}

	public virtual void RecomputeScroll()
	{
		GetScrollValue( out var horizontal, out var vertical );

		_client.SetPos( -horizontal, -vertical );
		_clientClip.Repaint();
	}
}
