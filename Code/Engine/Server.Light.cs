using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Server
{
	public const int MaxLightStyles = 64;

	private const int UnlitEntityIllum = 0x80;

	public string[] LightStyles { get; } = new string[MaxLightStyles];

	public void LightStyle( int style, string value )
	{
		LightStyles[style] = value;

		if ( _loading )
			return;

		foreach ( var client in Clients )
		{
			if ( (!client.Active && !client.Spawned) || client.FakeClient )
				continue;

			client.Message.WriteChar( LightStyleMessage );
			client.Message.WriteChar( style );
			client.Message.WriteString( value );
		}
	}

	public int GetEntityIllum( Edict edict )
	{
		if ( edict is null )
			return -1;

		if ( edict.Index <= MaxClients )
			return edict.LightLevel;

		if ( Client.Current is not { Connected: true } client )
			return UnlitEntityIllum;

		if ( client.GetEntity( edict.Index ) is not { } entity )
			return 0;

		return (entity.FloorColor.z + entity.FloorColor.y + entity.FloorColor.x) / 3;
	}
}
