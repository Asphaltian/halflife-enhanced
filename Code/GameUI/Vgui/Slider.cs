namespace GoldSrc.GameUI;

public class Slider : Panel
{
	private const int DefaultNumTicks = 10;
	private const float ArrowStep = 0.02f;

	private readonly int[] _nobPos = new int[2];
	private readonly int[] _nobDragStartPos = new int[2];

	private bool _dragging;
	private int _dragStartX;
	private int _minimum;
	private int _maximum;
	private int _value;
	private Border _sliderBorder;
	private Border _insetBorder;
	private float _nobSize;
	private TextImage _leftCaption;
	private TextImage _rightCaption;
	private int _numTicks = DefaultNumTicks;

	public Slider( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_nobSize = Scaled( 8 );

		RecomputeNobPosFromValue();

		AddActionSignalTarget( this );
	}

	public int Value => _value;

	public (int Minimum, int Maximum) Range => (_minimum, _maximum);

	public virtual void SetValue( int value, bool triggerChangeMessage = true )
	{
		var oldValue = _value;

		if ( value < _minimum )
			value = _minimum;

		if ( value > _maximum )
			value = _maximum;

		_value = value;

		RecomputeNobPosFromValue();

		if ( _value != oldValue && triggerChangeMessage )
			SendSliderMovedMessage();
	}

	public void SetRange( int minimum, int maximum )
	{
		if ( maximum < minimum )
			maximum = minimum;

		if ( minimum > maximum )
			minimum = maximum;

		_minimum = minimum;
		_maximum = maximum;

		if ( _value < _minimum )
			_value = _minimum;
		else if ( _value > _maximum )
			_value = _maximum;
	}

	public void SetTickCaptions( string left, string right )
	{
		if ( left is not null )
		{
			if ( _leftCaption is null )
				_leftCaption = new TextImage( Vgui, left );
			else
				_leftCaption.SetText( left );
		}

		if ( right is not null )
		{
			if ( _rightCaption is null )
				_rightCaption = new TextImage( Vgui, right );
			else
				_rightCaption.SetText( right );
		}

		InvalidateLayout();
	}

	public void SetNumTicks( int ticks ) => _numTicks = ticks;

	public void SetThumbWidth( int width ) => _nobSize = width;

	protected (int X, int Y, int Wide, int Tall) GetTrackRect()
	{
		var (wide, _) = PaintSize;

		return (0, Scaled( 8 ), (int)(wide - _nobSize), Scaled( 4 ));
	}

	private void RecomputeNobPosFromValue()
	{
		var (wide, _) = PaintSize;
		var position = ((float)(_value - _minimum) / (_maximum - _minimum)) * (wide - _nobSize);

		_nobPos[0] = (int)position;
		_nobPos[1] = (int)(_nobSize + position);

		if ( wide < _nobPos[1] )
		{
			_nobPos[1] = wide;
			_nobPos[0] = wide - (int)_nobSize;
		}
	}

	private void RecomputeValueFromNobPos()
	{
		var (wide, _) = PaintSize;

		_value = (int)((double)(((_nobPos[0] / (wide - _nobSize)) * (_maximum - _minimum)) + _minimum) + 0.5);
	}

	private void SendSliderMovedMessage() => PostActionSignal( new Message( "SliderMoved" ) { Int = _value } );

	public override void OnSizeChanged( int wide, int tall )
	{
		base.OnSizeChanged( wide, tall );

		RecomputeNobPosFromValue();
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "Slider/SliderFgColor" ) );

		_sliderBorder = Vgui.Scheme.GetBorder( "ButtonBorder" );
		_insetBorder = Vgui.Scheme.GetBorder( "ButtonDepressedBorder" );

		_leftCaption?.SetFont( GetFont( "DefaultVerySmall" ) );
		_rightCaption?.SetFont( GetFont( "DefaultVerySmall" ) );
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		var left = settings.GetString( "leftText", null );
		var right = settings.GetString( "rightText", null );
		var thumbWidth = settings.GetInt( "thumbwidth" );

		if ( thumbWidth != 0 )
			SetThumbWidth( thumbWidth );

		SetTickCaptions( left, right );
	}

	public override void PerformLayout()
	{
		RecomputeNobPosFromValue();

		if ( _leftCaption is not null )
		{
			var (wide, tall) = _leftCaption.GetContentSize();

			_leftCaption.SetSize( wide, tall );
		}

		if ( _rightCaption is not null )
		{
			var (wide, tall) = _rightCaption.GetContentSize();

			_rightCaption.SetSize( wide, tall );
		}
	}

	public override void OnCursorMoved( int x, int y )
	{
		if ( !_dragging )
			return;

		var cursor = Vgui.Input.CursorPos;

		(x, _) = ScreenToLocal( cursor.X, cursor.Y );

		var (wide, _) = PaintSize;

		_nobPos[0] = (_nobDragStartPos[0] - _dragStartX) + x;
		_nobPos[1] = x + (_nobDragStartPos[1] - _dragStartX);

		if ( wide < _nobPos[1] )
		{
			_nobPos[0] = (_nobPos[0] - _nobPos[1]) + wide;
			_nobPos[1] = wide;
		}

		if ( _nobPos[0] < 0 )
		{
			_nobPos[1] -= _nobPos[0];
			_nobPos[0] = 0;
		}

		RecomputeValueFromNobPos();
		SendSliderMovedMessage();
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled )
			return;

		var cursor = Vgui.Input.CursorPos;
		var (x, _) = ScreenToLocal( cursor.X, cursor.Y );

		RequestFocus();

		if ( x >= _nobPos[0] && x < _nobPos[1] )
		{
			_dragging = true;

			Vgui.Input.SetMouseCapture( this );

			_nobDragStartPos[0] = _nobPos[0];
			_nobDragStartPos[1] = _nobPos[1];
			_dragStartX = x;
			return;
		}

		SetValue( (int)(((float)(_maximum - _minimum) / Wide) * x) + _minimum );
	}

	public override void OnMouseDoublePressed( MouseCode code ) => OnMousePressed( code );

	public override void OnMouseReleased( MouseCode code )
	{
		_dragging = false;

		Vgui.Input.SetMouseCapture( null );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		var range = (float)_maximum - _minimum;

		switch ( code )
		{
			case KeyCode.Enter:
				break;

			case KeyCode.Home:
				SetValue( _minimum );
				break;

			case KeyCode.End:
				SetValue( _maximum );
				break;

			case KeyCode.PageUp:
				SetValue( (int)(range / _numTicks) + _value );
				break;

			case KeyCode.PageDown:
				SetValue( _value - (int)(range / _numTicks) );
				break;

			case KeyCode.Left:
				SetValue( _value - (int)(range * ArrowStep) );
				break;

			case KeyCode.Right:
				SetValue( (int)(range * ArrowStep) + _value );
				break;

			default:
				base.OnKeyCodeTyped( code );
				break;
		}
	}

	public override void PaintBackground()
	{
		base.PaintBackground();

		var surface = Vgui.Surface;
		var (x, y, wide, tall) = GetTrackRect();

		surface.DrawSetColor( GetSchemeColor( "SliderTrackColor" ) );
		surface.DrawFilledRect( x, y, x + wide, y + tall );

		_insetBorder?.Paint( surface, x, y, x + wide, y + tall );
	}

	public override void Paint()
	{
		DrawTicks();
		DrawTickLabels();

		var surface = Vgui.Surface;
		var (_, y, _, tall) = GetTrackRect();
		var half = Scaled( 16 ) / 2;

		surface.DrawSetColor( GetFgColor() );
		surface.DrawFilledRect( _nobPos[0], ((tall / 2) - half) + y, _nobPos[1], half + (tall / 2) + y );

		_sliderBorder?.Paint( surface, _nobPos[0], ((tall / 2) - half) + y, _nobPos[1], (tall / 2) + half + y );
	}

	private void DrawTicks()
	{
		var surface = Vgui.Surface;
		var (_, trackY, _, _) = GetTrackRect();
		var (wide, _) = PaintSize;
		var offset = _nobSize * 0.5f;
		var pixelsPerTick = (wide - _nobSize) / _numTicks;
		var y = trackY + (int)_nobSize;
		var tickHeight = Scaled( 5 );
		var one = Scaled( 1 );
		var two = Scaled( 2 );

		if ( Enabled )
		{
			surface.DrawSetColor( GetSchemeColor( "SliderTickColor" ) );

			for ( var i = 0; i <= _numTicks; i++ )
			{
				var x = (int)((i * pixelsPerTick) + offset);

				surface.DrawFilledRect( x, y, one + x, tickHeight + y );
			}

			return;
		}

		surface.DrawSetColor( GetSchemeColor( "DisabledFgColor1" ) );

		for ( var i = 0; i <= _numTicks; i++ )
		{
			var x = (int)((i * pixelsPerTick) + offset);

			surface.DrawFilledRect( one + x, one + y, two + x, one + tickHeight + y );
		}

		surface.DrawSetColor( GetSchemeColor( "DisabledFgColor2" ) );

		for ( var i = 0; i <= _numTicks; i++ )
		{
			var x = (int)((i * pixelsPerTick) + offset);

			surface.DrawFilledRect( x, y, one + x, tickHeight + y );
		}
	}

	private void DrawTickLabels()
	{
		var (_, trackY, _, _) = GetTrackRect();
		var (wide, _) = PaintSize;
		var y = trackY + Scaled( 12 );
		var color = GetSchemeColor( Enabled ? "SliderTickColor" : "DisabledFgColor1" );

		if ( _leftCaption is not null )
		{
			_leftCaption.SetPos( 0, y );
			_leftCaption.Color = color;
			_leftCaption.Paint( Vgui.Surface );
		}

		if ( _rightCaption is not null )
		{
			_rightCaption.SetPos( wide - _rightCaption.GetSize().Wide, y );
			_rightCaption.Color = color;
			_rightCaption.Paint( Vgui.Surface );
		}
	}
}
