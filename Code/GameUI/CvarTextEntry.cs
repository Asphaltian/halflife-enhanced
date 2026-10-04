using System;

namespace GoldSrc.GameUI;

public sealed class CvarTextEntry : TextEntry
{
	private const int MaxCvarText = 64;

	private readonly string _cvarName;

	private string _startValue = "";

	public CvarTextEntry( Vgui vgui, Panel parent, string name, string cvarName ) : base( vgui, parent, name )
	{
		_cvarName = cvarName;

		if ( _cvarName is not null && Cvar.GetString( _cvarName ) is { Length: > 0 } value )
		{
			SetText( value );

			_startValue = Truncate( value );
		}

		AddActionSignalTarget( this );
	}

	private static string Truncate( string value ) => value.Length < MaxCvarText ? value : value[..(MaxCvarText - 1)];

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		if ( GetMaximumCharCount() < 0 || GetMaximumCharCount() > MaxCvarText )
			SetMaximumCharCount( MaxCvarText - 1 );
	}

	public void ApplyChanges( bool immediate = false )
	{
		if ( _cvarName is null )
			return;

		var value = Truncate( Text );

		if ( value.Length == 0 )
			return;

		if ( immediate )
			Cvar.Set( _cvarName, value );
		else
			Client.ClientCmd( $"{_cvarName} \"{value}\"\n" );

		_startValue = value;
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "TextChanged" )
		{
			OnTextChanged();
			return;
		}

		base.OnMessage( message, from );
	}

	private void OnTextChanged()
	{
		if ( _cvarName is null )
			return;

		if ( !Truncate( Text ).Equals( _startValue, StringComparison.OrdinalIgnoreCase ) )
			PostActionSignal( new Message( "ControlModified" ) );
	}
}
