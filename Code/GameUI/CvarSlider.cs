using System.Globalization;

namespace GoldSrc.GameUI;

public sealed class CvarSlider : Slider
{
	private const float CvarScale = 100f;

	private readonly string _cvarName;
	private readonly bool _allowOutOfRange;

	private bool _modifiedOnce;
	private float _startValue;
	private int _startSliderValue;
	private int _lastSliderValue;
	private float _currentValue;

	public CvarSlider( Vgui vgui, Panel parent, string name, float minimum, float maximum, string cvarName, bool allowOutOfRange = false ) : base( vgui, parent, name )
	{
		AddActionSignalTarget( this );

		SetRange( (int)(minimum * CvarScale), (int)(maximum * CvarScale) );
		SetTickCaptions( minimum.ToString( "F2", CultureInfo.InvariantCulture ), maximum.ToString( "F2", CultureInfo.InvariantCulture ) );

		_cvarName = cvarName;
		_allowOutOfRange = allowOutOfRange;

		Reset();
	}

	public void Reset()
	{
		_startValue = Cvar.GetFloat( _cvarName );
		_currentValue = _startValue;

		SetValue( (int)(_startValue * CvarScale) );

		_startSliderValue = Value;
		_lastSliderValue = _startSliderValue;
	}

	public bool HasBeenModified()
	{
		if ( Value != _startSliderValue )
			_modifiedOnce = true;

		return _modifiedOnce;
	}

	public float GetSliderValue() => _allowOutOfRange ? _currentValue : Value / CvarScale;

	public void SetSliderValue( float value )
	{
		SetValue( (int)(value * CvarScale), false );

		_lastSliderValue = Value;

		if ( _currentValue != value )
		{
			_currentValue = value;
			_modifiedOnce = true;
		}
	}

	public void ApplyChanges()
	{
		if ( !_modifiedOnce )
			return;

		_startSliderValue = Value;
		_startValue = _allowOutOfRange ? _currentValue : _startSliderValue / CvarScale;

		Cvar.SetValue( _cvarName, _startValue );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "SliderMoved" )
		{
			OnSliderMoved();
			return;
		}

		base.OnMessage( message, from );
	}

	private void OnSliderMoved()
	{
		if ( Value == _startSliderValue )
		{
			if ( !_modifiedOnce )
				return;
		}
		else
		{
			_modifiedOnce = true;
		}

		if ( _lastSliderValue != Value )
		{
			_lastSliderValue = Value;
			_currentValue = _lastSliderValue / CvarScale;
		}

		PostActionSignal( new Message( "ControlModified" ) );
	}

	public override void Paint()
	{
		var value = Cvar.GetFloat( _cvarName );

		if ( value != _startValue )
		{
			_startValue = value;
			_currentValue = value;

			SetValue( (int)(value * CvarScale) );

			_startSliderValue = Value;
		}

		base.Paint();
	}
}
