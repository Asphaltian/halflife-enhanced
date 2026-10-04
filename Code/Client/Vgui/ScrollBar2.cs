using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public class Slider2 : Panel
{
	private sealed class Handler( Slider2 slider ) : InputSignal
	{
		public void CursorMoved( int x, int y, Panel panel ) => slider.PrivateCursorMoved( x, y, panel );

		public void CursorEntered( Panel panel )
		{
		}

		public void CursorExited( Panel panel )
		{
		}

		public void MouseDoublePressed( MouseCode code, Panel panel )
		{
		}

		public void MousePressed( MouseCode code, Panel panel ) => slider.PrivateMousePressed( code, panel );

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

	private readonly bool _vertical;
	private bool _dragging;
	private readonly int[] _nobPos = new int[2];
	private readonly int[] _nobDragStartPos = new int[2];
	private readonly int[] _dragStartPos = new int[2];
	private readonly List<IntChangeSignal> _intChangeSignalDar = [];
	private readonly int[] _range = new int[2];
	private int _value;
	private int _rangeWindow;
	private bool _rangeWindowEnabled;
	private int _buttonOffset;

	public Slider2( int x, int y, int wide, int tall, bool vertical ) : base( x, y, wide, tall )
	{
		_vertical = vertical;
		_dragging = false;
		_value = 0;
		_range[0] = 0;
		_range[1] = 299;
		_rangeWindow = 0;
		_rangeWindowEnabled = false;
		_buttonOffset = 0;

		RecomputeNobPosFromValue();
		AddInputSignal( new Handler( this ) );
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		RecomputeNobPosFromValue();
	}

	public virtual bool IsVertical() => _vertical;

	public virtual void SetValue( int value )
	{
		var oldValue = _value;

		if ( value < _range[0] )
			value = _range[0];

		if ( value > _range[1] )
			value = _range[1];

		_value = value;

		RecomputeNobPosFromValue();

		if ( _value != oldValue )
			FireIntChangeSignal();
	}

	public virtual int GetValue() => _value;

	private void RecomputeNobPosFromValue()
	{
		GetPaintSize( out var wide, out var tall );

		float fwide = wide;
		float ftall = tall;
		float frange = _range[1] - _range[0];
		float fvalue = _value - _range[0];
		var fper = fvalue / frange;
		float frangewindow = _rangeWindow;

		if ( frangewindow < 0 )
			frangewindow = 0;

		if ( !_rangeWindowEnabled )
			frangewindow = frange;

		if ( frangewindow > 0 )
		{
			if ( _vertical )
			{
				var fnobsize = frangewindow;
				var freepixels = ftall - fnobsize;
				var firstpixel = freepixels * fper;

				_nobPos[0] = (int)firstpixel;
				_nobPos[1] = (int)(firstpixel + fnobsize);

				if ( _nobPos[1] > tall )
				{
					_nobPos[0] = tall - (int)fnobsize;
					_nobPos[1] = tall;
				}
			}
			else
			{
				var fnobsize = frangewindow;
				var freepixels = fwide - fnobsize;
				var firstpixel = freepixels * fper;

				_nobPos[0] = (int)firstpixel;
				_nobPos[1] = (int)(firstpixel + fnobsize);

				if ( _nobPos[1] > wide )
				{
					_nobPos[0] = wide - (int)fnobsize;
					_nobPos[1] = wide;
				}
			}
		}

		Repaint();
	}

	private void RecomputeValueFromNobPos()
	{
		GetPaintSize( out var wide, out var tall );

		float fwide = wide;
		float ftall = tall;
		float frange = _range[1] - _range[0];
		float fvalue = _value - _range[0];
		float fnob = _nobPos[0];
		float frangewindow = _rangeWindow;

		if ( frangewindow < 0 )
			frangewindow = 0;

		if ( !_rangeWindowEnabled )
			frangewindow = frange;

		if ( frangewindow > 0 )
		{
			if ( _vertical )
			{
				var fnobsize = frangewindow;

				fvalue = frange * (fnob / (ftall - fnobsize));
			}
			else
			{
				var fnobsize = frangewindow;

				fvalue = frange * (fnob / (fwide - fnobsize));
			}
		}

		_value = (int)(fvalue + _range[0] + 0.5);

		_value = _value < _range[1] ? _value : _range[1];
	}

	public virtual bool HasFullRange()
	{
		GetPaintSize( out var wide, out var tall );

		float fwide = wide;
		float ftall = tall;
		float frange = _range[1] - _range[0];
		float frangewindow = _rangeWindow;

		if ( frangewindow < 0 )
			frangewindow = 0;

		if ( !_rangeWindowEnabled )
			frangewindow = frange;

		if ( frangewindow > 0 )
		{
			if ( _vertical )
			{
				if ( frangewindow <= ftall + _buttonOffset )
					return true;
			}
			else
			{
				if ( frangewindow <= fwide + _buttonOffset )
					return true;
			}
		}

		return false;
	}

	public virtual void AddIntChangeSignal( IntChangeSignal s )
	{
		if ( !_intChangeSignalDar.Contains( s ) )
			_intChangeSignalDar.Add( s );
	}

	protected virtual void FireIntChangeSignal()
	{
		for ( var i = 0; i < _intChangeSignalDar.Count; i++ )
			_intChangeSignalDar[i].IntChanged( GetValue(), this );
	}

	public override void PaintBackground()
	{
		GetPaintSize( out var wide, out var tall );

		if ( _vertical )
		{
			DrawSetColor( 40, 40, 40, 0 );
			DrawFilledRect( 0, 0, wide, tall );

			DrawSetColor( 0, 0, 0, 0 );
			DrawFilledRect( 0, _nobPos[0], wide, _nobPos[1] );

			DrawSetColor( 60, 60, 60, 0 );
			DrawFilledRect( 0, _nobPos[0], wide, _nobPos[0] + 1 );
			DrawFilledRect( 0, _nobPos[1], wide, _nobPos[1] + 1 );
			DrawFilledRect( 0, _nobPos[0] + 1, 1, _nobPos[1] );
			DrawFilledRect( wide - 1, _nobPos[0] + 1, wide, _nobPos[1] );
		}
		else
		{
			DrawSetColor( SchemeColor.Secondary3 );
			DrawFilledRect( 0, 0, wide, tall );

			DrawSetColor( SchemeColor.Black );
			DrawOutlinedRect( 0, 0, wide, tall );

			DrawSetColor( SchemeColor.Primary2 );
			DrawFilledRect( _nobPos[0], 0, _nobPos[1], tall );

			DrawSetColor( SchemeColor.Black );
			DrawOutlinedRect( _nobPos[0], 0, _nobPos[1], tall );
		}
	}

	public virtual void SetRange( int min, int max )
	{
		if ( max < min )
			max = min;

		if ( min > max )
			min = max;

		_range[0] = min;
		_range[1] = max;
	}

	public virtual void GetRange( out int min, out int max )
	{
		min = _range[0];
		max = _range[1];
	}

	public virtual void PrivateCursorMoved( int x, int y, Panel panel )
	{
		if ( !_dragging )
			return;

		GetApp().GetCursorPos( out x, out y );
		ScreenToLocal( ref x, ref y );

		GetPaintSize( out var wide, out var tall );

		if ( _vertical )
		{
			_nobPos[0] = _nobDragStartPos[0] + (y - _dragStartPos[1]);
			_nobPos[1] = _nobDragStartPos[1] + (y - _dragStartPos[1]);

			if ( _nobPos[1] > tall )
			{
				_nobPos[0] = tall - (_nobPos[1] - _nobPos[0]);
				_nobPos[1] = tall;
			}

			if ( _nobPos[0] < 0 )
			{
				_nobPos[1] = _nobPos[1] - _nobPos[0];
				_nobPos[0] = 0;
			}
		}
		else
		{
			_nobPos[0] = _nobDragStartPos[0] + (x - _dragStartPos[0]);
			_nobPos[1] = _nobDragStartPos[1] + (x - _dragStartPos[0]);

			if ( _nobPos[1] > wide )
			{
				_nobPos[0] = wide - (_nobPos[1] - _nobPos[0]);
				_nobPos[1] = wide;
			}

			if ( _nobPos[0] < 0 )
			{
				_nobPos[1] = _nobPos[1] - _nobPos[0];
				_nobPos[0] = 0;
			}
		}

		RecomputeValueFromNobPos();
		Repaint();
		FireIntChangeSignal();
	}

	public virtual void PrivateMousePressed( MouseCode code, Panel panel )
	{
		GetApp().GetCursorPos( out var x, out var y );
		ScreenToLocal( ref x, ref y );

		if ( _vertical )
		{
			if ( y >= _nobPos[0] && y < _nobPos[1] )
			{
				_dragging = true;
				GetApp().SetMouseCapture( this );
				_nobDragStartPos[0] = _nobPos[0];
				_nobDragStartPos[1] = _nobPos[1];
				_dragStartPos[0] = x;
				_dragStartPos[1] = y;
			}
		}
		else
		{
			if ( x >= _nobPos[0] && x < _nobPos[1] )
			{
				_dragging = true;
				GetApp().SetMouseCapture( this );
				_nobDragStartPos[0] = _nobPos[0];
				_nobDragStartPos[1] = _nobPos[1];
				_dragStartPos[0] = x;
				_dragStartPos[1] = y;
			}
		}
	}

	public virtual void PrivateMouseReleased( MouseCode code, Panel panel )
	{
		_dragging = false;
		GetApp().SetMouseCapture( null );
	}

	public virtual void GetNobPos( out int min, out int max )
	{
		min = _nobPos[0];
		max = _nobPos[1];
	}

	public virtual void SetRangeWindow( int rangeWindow )
	{
		_rangeWindow = rangeWindow;
	}

	public virtual void SetRangeWindowEnabled( bool state )
	{
		_rangeWindowEnabled = state;
	}

	public virtual void SetButtonOffset( int buttonOffset )
	{
		_buttonOffset = buttonOffset;
	}
}

public class ScrollBar2 : Panel
{
	private sealed class SliderSignal( ScrollBar2 scrollBar ) : IntChangeSignal
	{
		public void IntChanged( int value, Panel panel ) => scrollBar.FireIntChangeSignal();
	}

	private sealed class ButtonSignal( ScrollBar2 scrollBar, int buttonIndex ) : ActionSignal
	{
		public void ActionPerformed( Panel panel ) => scrollBar.DoButtonPressed( buttonIndex );
	}

	private sealed class ScrollBarButton : Button
	{
		private readonly LineBorder _lineBorder = new( new Color( 60, 60, 60, 0 ) );

		public ScrollBarButton( string fileName, int x, int y, int wide, int tall ) : base( "", x, y, wide, tall )
		{
			Image image = VguiLoadTga.LoadTga( fileName );

			if ( image is not null )
			{
				image.SetColor( new Color( 140, 140, 140, 0 ) );
				SetImage( image );
			}

			SetBorder( _lineBorder );
		}

		public override void PaintBackground()
		{
			GetPaintSize( out var wide, out var tall );

			DrawSetColor( 0, 0, 0, 0 );
			DrawFilledRect( 0, 0, wide, tall );
		}
	}

	protected readonly Button[] _button = new Button[2];
	protected Slider2 _slider;
	protected readonly List<IntChangeSignal> _intChangeSignalDar = [];
	protected int _buttonPressedScrollValue;

	public ScrollBar2( int x, int y, int wide, int tall, bool vertical ) : base( x, y, wide, tall )
	{
		_slider = null;
		_button[0] = null;
		_button[1] = null;

		if ( vertical )
		{
			SetSlider( new Slider2( 0, wide - 1, wide, tall - (wide * 2) + 2, true ) );
			SetButton( new ScrollBarButton( "gfx/vgui/arrowup.tga", 0, 0, wide, wide ), 0 );
			SetButton( new ScrollBarButton( "gfx/vgui/arrowdown.tga", 0, tall - wide, wide, wide ), 1 );
		}
		else
		{
			SetSlider( new Slider2( tall, 0, wide - (tall * 2), tall, false ) );
			SetButton( new ScrollBarButton( "gfx/vgui/320_arrowlt.tga", 0, 0, tall + 1, tall + 1 ), 0 );
			SetButton( new ScrollBarButton( "gfx/vgui/320_arrowrt.tga", wide - tall, 0, tall + 1, tall + 1 ), 1 );
		}

		SetPaintBorderEnabled( true );
		SetPaintBackgroundEnabled( true );
		SetPaintEnabled( true );
		SetButtonPressedScrollValue( 15 );

		Validate();
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		if ( _slider is null )
			return;

		if ( _button[0] is null )
			return;

		if ( _button[1] is null )
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

	public override void PerformLayout()
	{
	}

	public virtual void SetValue( int value ) => _slider.SetValue( value );

	public virtual int GetValue() => _slider.GetValue();

	public virtual void AddIntChangeSignal( IntChangeSignal s )
	{
		if ( !_intChangeSignalDar.Contains( s ) )
			_intChangeSignalDar.Add( s );

		_slider.AddIntChangeSignal( new SliderSignal( this ) );
	}

	public virtual void SetRange( int min, int max ) => _slider.SetRange( min, max );

	public virtual void FireIntChangeSignal()
	{
		for ( var i = 0; i < _intChangeSignalDar.Count; i++ )
			_intChangeSignalDar[i].IntChanged( _slider.GetValue(), this );
	}

	public virtual bool IsVertical() => _slider.IsVertical();

	public virtual bool HasFullRange() => _slider.HasFullRange();

	public virtual void SetButton( Button button, int index )
	{
		if ( _button[index] is not null )
			RemoveChild( _button[index] );

		_button[index] = button;
		AddChild( _button[index] );

		_button[index].AddActionSignal( new ButtonSignal( this, index ) );

		Validate();
	}

	public virtual Button GetButton( int index ) => _button[index];

	public virtual void SetSlider( Slider2 slider )
	{
		if ( _slider is not null )
			RemoveChild( _slider );

		_slider = slider;
		AddChild( _slider );

		_slider.AddIntChangeSignal( new SliderSignal( this ) );

		Validate();
	}

	public virtual Slider2 GetSlider() => _slider;

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

	public virtual void SetRangeWindow( int rangeWindow ) => _slider.SetRangeWindow( rangeWindow );

	public virtual void SetRangeWindowEnabled( bool state ) => _slider.SetRangeWindowEnabled( state );

	public virtual void Validate()
	{
		if ( _slider is not null )
		{
			var buttonOffset = 0;

			for ( var i = 0; i < 2; i++ )
			{
				if ( _button[i] is not null )
				{
					if ( _button[i].IsVisible() )
					{
						if ( _slider.IsVertical() )
							buttonOffset += _button[i].GetTall();
						else
							buttonOffset += _button[i].GetWide();
					}
				}
			}

			_slider.SetButtonOffset( buttonOffset );
		}

		GetSize( out var wide, out var tall );
		SetSize( wide, tall );
	}
}
