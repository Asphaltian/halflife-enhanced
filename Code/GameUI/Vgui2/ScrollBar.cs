using System;

namespace GoldSrc.GameUI;

public sealed class ScrollBarSlider : Panel
{
	private readonly bool _vertical;

	private bool _dragging;
	private int _nobStart;
	private int _nobEnd;
	private int _nobDragStart;
	private int _nobDragEnd;
	private int _dragStartX;
	private int _dragStartY;
	private int _rangeMin;
	private int _rangeMax;
	private int _value;
	private int _rangeWindow;
	private Border _sliderBorder;

	public ScrollBarSlider( Vgui vgui, Panel parent, string name, bool vertical ) : base( vgui, parent, name )
	{
		_vertical = vertical;

		RecomputeNobPosFromValue();
	}

	public bool IsVertical => _vertical;

	public int Value => _value;

	public void SetValue( int value )
	{
		var oldValue = _value;

		_value = Math.Min( Math.Max( value, _rangeMin ), _rangeMax - _rangeWindow );

		RecomputeNobPosFromValue();

		if ( _value != oldValue )
			SendScrollBarSliderMovedMessage();
	}

	public void SetRange( int min, int max )
	{
		_rangeMax = Math.Max( min, max );
		_rangeMin = Math.Min( min, _rangeMax );

		InvalidateLayout();
	}

	public int RangeWindow => _rangeWindow;

	public void SetRangeWindow( int rangeWindow ) => _rangeWindow = rangeWindow;

	private void RecomputeNobPosFromValue()
	{
		var (wide, tall) = PaintSize;
		var range = (float)(_rangeMax - _rangeMin);
		var window = (float)_rangeWindow;

		if ( window <= 0f )
			return;

		var length = _vertical ? tall - 1f : wide;
		var nob = Math.Max( _vertical ? wide : tall - 1f, window / (range <= 0f ? 1f : range) * length );
		var position = (length - nob) * ((_value - _rangeMin) / (range - window));

		_nobStart = (int)position;
		_nobEnd = (int)(position + nob);

		if ( length < _nobEnd )
		{
			_nobStart = (int)(length - nob);
			_nobEnd = (int)length;
		}
	}

	private void RecomputeValueFromNobPos()
	{
		var (wide, tall) = PaintSize;
		var range = (float)(_rangeMax - _rangeMin);
		var window = (float)_rangeWindow;
		var value = (float)(_value - _rangeMin);

		if ( window > 0f )
		{
			if ( range <= 0f )
				range = 1f;

			var length = (float)(_vertical ? tall : wide);
			var nob = Math.Max( _vertical ? wide : tall, window / range * length );

			value = _nobStart / (length - nob) * (range - window);
		}

		var last = _rangeMax - _rangeWindow;
		var rounded = (int)(_rangeMin + value + 0.5);

		_value = range * 0.01f <= Math.Abs( (double)(window + value - _rangeMax) ) && rounded < last ? rounded : last;
	}

	private void SendScrollBarSliderMovedMessage() => PostActionSignal( new Message( "ScrollBarSliderMoved" ) { Int = _value } );

	public override void OnCursorMoved( int x, int y )
	{
		if ( !_dragging )
			return;

		var (wide, tall) = PaintSize;

		if ( !_vertical )
		{
			_nobStart = _nobDragStart - _dragStartX + x;
			_nobEnd = _nobDragEnd - _dragStartX + x;

			if ( wide < _nobEnd )
			{
				_nobStart = _nobStart - _nobEnd + wide;
				_nobEnd = wide;
			}
		}
		else
		{
			_nobStart = _nobDragStart - _dragStartY + y;
			_nobEnd = _nobDragEnd - _dragStartY + y;

			if ( tall < _nobEnd )
			{
				_nobStart = _nobStart - _nobEnd + tall;
				_nobEnd = tall;

				SetValue( _rangeMax - _rangeWindow );
			}
		}

		if ( _nobStart < 0 )
		{
			_nobEnd -= _nobStart;
			_nobStart = 0;

			SetValue( 0 );
		}

		InvalidateLayout();
		RecomputeValueFromNobPos();
		SendScrollBarSliderMovedMessage();
	}

	public override void OnMousePressed( MouseCode code )
	{
		var (cursorX, cursorY) = Vgui.Input.CursorPos;
		var (x, y) = ScreenToLocal( cursorX, cursorY );
		var position = _vertical ? y : x;

		if ( position < _nobStart )
		{
			SetValue( _value - _rangeWindow );
			return;
		}

		if ( position >= _nobEnd )
		{
			SetValue( _value + _rangeWindow );
			return;
		}

		_dragging = true;

		Vgui.Input.SetMouseCapture( this );

		_nobDragStart = _nobStart;
		_nobDragEnd = _nobEnd;
		_dragStartX = x;
		_dragStartY = y;
	}

	public override void OnMouseDoublePressed( MouseCode code ) => OnMousePressed( code );

	public override void OnMouseReleased( MouseCode code )
	{
		_dragging = false;

		Vgui.Input.SetMouseCapture( null );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "ScrollBarSlider/ScrollBarSliderFgColor" ) );
		SetBgColor( GetSchemeColor( "ScrollBarSlider/ScrollBarSliderBgColor" ) );

		_sliderBorder = Vgui.Scheme.GetBorder( "ButtonBorder" );
	}

	public override void PerformLayout() => RecomputeNobPosFromValue();

	public override void PaintBackground()
	{
		var (wide, tall) = PaintSize;

		Vgui.Surface.DrawSetColor( GetBgColor() );
		Vgui.Surface.DrawFilledRect( 0, 0, wide - 1, tall - 1 );
	}

	public override void Paint()
	{
		var (wide, tall) = PaintSize;
		var range = _rangeMax - _rangeMin;

		if ( range <= 0 || _rangeWindow >= range )
			return;

		var surface = Vgui.Surface;

		surface.DrawSetColor( GetFgColor() );

		if ( _vertical )
		{
			surface.DrawFilledRect( 0, _nobStart, wide - 1, _nobEnd );

			_sliderBorder?.Paint( surface, 0, _nobStart, wide - 1, _nobEnd );
		}
		else
		{
			surface.DrawFilledRect( _nobStart, 0, _nobEnd, tall );

			_sliderBorder?.Paint( surface, _nobStart - 1, 1, _nobEnd, tall );
		}
	}
}

public sealed class ScrollBarButton : Button
{
	private readonly Image _image;

	public ScrollBarButton( Vgui vgui, string imageName ) : base( vgui, null, null, "" )
	{
		SetButtonActivationType( ActivationType.OnPressed );

		_image = new BitmapImage( vgui.Surface.LoadImage( imageName ) );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetDefaultBorder( Vgui.Scheme.GetBorder( "ScrollBarButtonBorder" ) );
		SetDepressedBorder( Vgui.Scheme.GetBorder( "ScrollBarButtonDepressedBorder" ) );
		SetArmedColor( GetFgColor(), GetBgColor() );
		SetDepressedColor( GetFgColor(), GetBgColor() );
		SetDefaultColor( GetSchemeColor( "DimBaseText" ), GetBgColor() );
		SetContentAlignment( Alignment.Center );
		AddImage( _image, 0 );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled || !IsMouseClickEnabled( code ) || !IsUseCaptureMouseEnabled )
			return;

		SetSelected( true );

		Vgui.Input.SetMouseCapture( this );
	}

	public override void OnMouseReleased( MouseCode code )
	{
		if ( !Enabled || !IsMouseClickEnabled( code ) || !IsUseCaptureMouseEnabled )
			return;

		SetSelected( false );

		Vgui.Input.SetMouseCapture( null );
	}
}

public sealed class ScrollBar : Panel
{
	private const int ButtonCount = 2;
	private const int ScrollDelay = 400;
	private const int ScrollRepeatDelay = 50;

	private readonly ScrollBarButton[] _buttons = new ScrollBarButton[ButtonCount];
	private readonly ScrollBarSlider _slider;

	private int _buttonPressedScrollValue = 20;
	private int _scrollDelay = ScrollDelay;
	private bool _respond = true;

	public ScrollBar( Vgui vgui, Panel parent, string name, bool vertical ) : base( vgui, parent, name )
	{
		_slider = new ScrollBarSlider( vgui, null, null, vertical );
		_slider.AddActionSignalTarget( this );
		_slider.SetParent( this );

		SetButton( new ScrollBarButton( vgui, vertical ? "resource/icon_up" : "resource/icon_left" ), 0 );
		SetButton( new ScrollBarButton( vgui, vertical ? "resource/icon_down" : "resource/icon_right" ), 1 );

		if ( vertical )
		{
			_buttons[0].SetTextInset( Scaled( -8 ), 0 );
			_buttons[1].SetTextInset( Scaled( -8 ), 0 );
		}

		SetPaintBorderEnabled( true );
		SetPaintBackgroundEnabled( false );
		SetPaintEnabled( true );
	}

	private static int TimeMillis => (int)(RealTime.Now * 1000f);

	public int Value => _slider.Value;

	public void SetValue( int value ) => _slider.SetValue( value );

	public void SetRange( int min, int max ) => _slider.SetRange( min, max );

	public int RangeWindow => _slider.RangeWindow;

	public void SetRangeWindow( int rangeWindow ) => _slider.SetRangeWindow( rangeWindow );

	public void SetButtonPressedScrollValue( int value ) => _buttonPressedScrollValue = value;

	private void SetButton( ScrollBarButton button, int index )
	{
		_buttons[index]?.SetParent( null );
		_buttons[index] = button;

		button.SetParent( this );
		button.AddActionSignalTarget( this );
		button.SetCommand( new Message( "ScrollButtonPressed" ) { Int = index } );
	}

	private void SendSliderMoveMessage( int value ) => PostActionSignal( new Message( "ScrollBarSliderMoved" ) { Int = value } );

	private void OnButtonPressed( int direction )
	{
		if ( !_respond )
			return;

		var value = (_buttonPressedScrollValue * direction) + _slider.Value;

		_slider.SetValue( value );

		SendSliderMoveMessage( value );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ScrollBarSliderMoved" )
			SendSliderMoveMessage( message.Int );
		else
			base.OnMessage( message, from );
	}

	public override void OnMouseFocusTicked()
	{
		int direction;

		if ( _buttons[0].IsDepressed )
		{
			direction = -1;
		}
		else if ( _buttons[1].IsDepressed )
		{
			direction = 1;
		}
		else
		{
			_scrollDelay = TimeMillis + ScrollDelay;
			_respond = true;
			return;
		}

		OnButtonPressed( direction );

		if ( _scrollDelay < TimeMillis )
		{
			_scrollDelay = TimeMillis + ScrollRepeatDelay;
			_respond = true;
		}
		else
		{
			_respond = false;
		}
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		InvalidateLayout();

		_slider.InvalidateLayout();
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBgColor( GetSchemeColor( "ScrollBar/BgColor" ) );
	}

	public override void PerformLayout()
	{
		var (wide, tall) = PaintSize;

		if ( _slider.IsVertical )
		{
			_slider.SetBounds( 0, wide, wide, tall - (wide * 2) + 1 );
			_buttons[0].SetBounds( 0, 0, wide - 1, wide );
			_buttons[1].SetBounds( 0, tall - wide, wide - 1, wide );
		}
		else
		{
			_slider.SetBounds( tall, -1, wide - (tall * 2) + 1, tall + 1 );
			_buttons[0].SetBounds( 0, 0, tall, tall );
			_buttons[1].SetBounds( wide - tall, 0, tall, tall );
		}

		_slider.InvalidateLayout();
	}
}
