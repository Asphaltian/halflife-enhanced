using System;

namespace GoldSrc.GameUI;

public sealed class CvarToggleCheckButton : CheckButton
{
	private readonly string _cvarName;

	private bool _startValue;

	public CvarToggleCheckButton( Vgui vgui, Panel parent, string name, string text, string cvarName ) : base( vgui, parent, name, text )
	{
		_cvarName = cvarName;

		if ( _cvarName is not null )
		{
			_startValue = Cvar.GetFloat( _cvarName ) > 0f;

			SetSelected( _startValue );
		}

		AddActionSignalTarget( this );
	}

	public void Reset()
	{
		_startValue = Cvar.GetFloat( _cvarName ) > 0f;

		SetSelected( _startValue );
	}

	public void ApplyChanges()
	{
		_startValue = IsSelected;

		Cvar.SetValue( _cvarName, _startValue ? 1f : 0f );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "CheckButtonChecked" )
		{
			if ( IsSelected != _startValue )
				PostActionSignal( new Message( "ControlModified" ) );

			return;
		}

		base.OnMessage( message, from );
	}

	public override void Paint()
	{
		if ( _cvarName is { Length: > 0 } )
		{
			var value = Cvar.GetFloat( _cvarName ) > 0f;

			if ( value != _startValue )
			{
				SetSelected( value );

				_startValue = value;
			}
		}

		base.Paint();
	}
}

public sealed class CvarNegateCheckButton : CheckButton
{
	private const float MinimumValue = 0.00001f;
	private const float DefaultValue = 0.022f;

	private readonly string _cvarName;

	private bool _startValue;

	public CvarNegateCheckButton( Vgui vgui, Panel parent, string name, string text, string cvarName ) : base( vgui, parent, name, text )
	{
		_cvarName = cvarName;

		Reset();

		AddActionSignalTarget( this );
	}

	public void Reset()
	{
		_startValue = Cvar.GetFloat( _cvarName ) < 0f;

		SetSelected( _startValue );
	}

	public void ApplyChanges()
	{
		if ( _cvarName is not { Length: > 0 } )
			return;

		var value = MathF.Abs( Cvar.GetFloat( _cvarName ) );

		if ( value < MinimumValue )
			value = DefaultValue;

		_startValue = IsSelected;

		Cvar.SetValue( _cvarName, _startValue ? -value : value );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "CheckButtonChecked" )
		{
			if ( IsSelected != _startValue )
				PostActionSignal( new Message( "ControlModified" ) );

			return;
		}

		base.OnMessage( message, from );
	}

	public override void Paint()
	{
		if ( _cvarName is not null )
		{
			var negative = Cvar.GetFloat( _cvarName ) < 0f;

			if ( negative != _startValue )
			{
				SetSelected( negative );

				_startValue = negative;
			}
		}

		base.Paint();
	}
}
