using System;
using System.IO;

namespace GoldSrc;

partial class Server
{
	public const int DeltaDescriptionMessage = 14;

	private const string DeltaFile = "delta.lst";

	private static List<(string Name, Delta Delta)> _deltas = [];

	private static Delta _playerDelta;
	private static Delta _entityDelta;
	private static Delta _customEntityDelta;
	private static Delta _clientDelta;
	private static Delta _weaponDelta;
	private static Delta _eventDelta;

	private ServerClient _hostClient;

	public int CurrentPlayer => _hostClient is { } client && client.Slot < MaxClients ? client.Slot : -1;

	public static Delta LookupDelta( string name )
	{
		foreach ( var (registered, delta) in _deltas )
		{
			if ( registered.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return delta;
		}

		throw new InvalidDataException( $"Couldn't find delta for {name}\n" );
	}

	private static void RegisterDelta( string name, string fileName )
	{
		if ( Delta.Load( name, fileName ) is not { } delta )
			throw new InvalidDataException( $"Error parsing {fileName}!!!\n" );

		_deltas.Insert( 0, (name, delta) );
	}

	private void InitDeltas()
	{
		if ( _eventDelta is not null )
			return;

		Con.DPrintf( "Initializing deltas\n" );

		_deltas.Clear();

		Delta.ClearEncoders();

		RegisterDelta( "clientdata_t", DeltaFile );
		RegisterDelta( "entity_state_t", DeltaFile );
		RegisterDelta( "entity_state_player_t", DeltaFile );
		RegisterDelta( "custom_entity_state_t", DeltaFile );
		RegisterDelta( "usercmd_t", DeltaFile );
		RegisterDelta( "weapon_data_t", DeltaFile );
		RegisterDelta( "event_t", DeltaFile );

		_playerDelta = LookupDelta( "entity_state_player_t" );
		_entityDelta = LookupDelta( "entity_state_t" );
		_customEntityDelta = LookupDelta( "custom_entity_state_t" );
		_clientDelta = LookupDelta( "clientdata_t" );
		_weaponDelta = LookupDelta( "weapon_data_t" );
		_eventDelta = LookupDelta( "event_t" );

		GameDll.RegisterEncoders();

		foreach ( var (_, delta) in _deltas )
		{
			if ( delta.ConditionalEncodeName.Length != 0 )
				delta.ConditionalEncode = Delta.LookupEncoder( delta.ConditionalEncodeName );
		}
	}

	private static void WriteDeltaDescriptions( SizeBuf buffer )
	{
		foreach ( var (name, delta) in _deltas )
		{
			buffer.WriteByte( DeltaDescriptionMessage );
			buffer.WriteString( name );
			buffer.StartBitWriting();

			delta.WriteDescription( buffer );

			buffer.EndBitWriting();
		}
	}
}
