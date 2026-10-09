using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "worldspawn" )]
public class World : BaseEntity
{
	private const int SpawnFlagDark = 0x0001;
	private const int SpawnFlagTitle = 0x0002;
	private const int SpawnFlagForceTeam = 0x0004;

	public string MapTeams { get; private set; }

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "skyname":
				Cvar.Set( "sv_skyname", value );
				return true;
			case "sounds":
				Server.Globals.CdAudioTrack = Util.Atoi( value );
				return true;
			case "WaveHeight":
				Scale = Util.Atof( value ) * (1f / 8f);
				Cvar.SetValue( "sv_wateramp", Scale );
				return true;
			case "MaxRange":
				Speed = Util.Atof( value );
				return true;
			case "chaptertitle":
				NetName = Server.AllocString( value );
				return true;
			case "startdark":
				if ( Util.Atoi( value ) != 0 )
					SpawnFlags |= SpawnFlagDark;
				return true;
			case "newunit":
				if ( Util.Atoi( value ) != 0 )
					Cvar.SetValue( "sv_newunit", 1f );
				return true;
			case "gametitle":
				if ( Util.Atoi( value ) != 0 )
					SpawnFlags |= SpawnFlagTitle;
				return true;
			case "mapteams":
				MapTeams = Server.AllocString( value );
				return true;
			case "defaultteam":
				if ( Util.Atoi( value ) != 0 )
					SpawnFlags |= SpawnFlagForceTeam;
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		HalfLifeDll.GameOver = false;
		Precache();
	}

	public override void Precache()
	{
		HalfLifeDll.LastSpawn = null;

		Cvar.Set( "sv_gravity", "800" );
		Cvar.Set( "sv_stepsize", "18" );
		Cvar.Set( "room_type", "0" );

		HalfLifeDll.GameRules = GameRules.Install( Server );

		SoundEnt.Current = Server.CreateEntity<SoundEnt>();
		SoundEnt.Current.Spawn();

		HalfLifeDll.InitBodyQue( Server );

		Sentences.Init();
		TextureType.Init();

		BasePlayerItem.Precache( Server );
		HalfLifeDll.ClientPrecache( Server );

		Server.PrecacheSound( "common/null.wav" );

		Server.PrecacheSound( "items/suitchargeok1.wav" );
		Server.PrecacheSound( "items/gunpickup2.wav" );

		Server.PrecacheSound( "common/bodydrop3.wav" );
		Server.PrecacheSound( "common/bodydrop4.wav" );

		HalfLifeDll.Language = (int)Cvar.GetFloat( "sv_language" );

		if ( HalfLifeDll.Language == HalfLifeDll.LanguageGerman )
		{
			Server.PrecacheModel( "models/germangibs.mdl" );
		}
		else
		{
			Server.PrecacheModel( "models/hgibs.mdl" );
			Server.PrecacheModel( "models/agibs.mdl" );
		}

		Server.PrecacheSound( "weapons/ric1.wav" );
		Server.PrecacheSound( "weapons/ric2.wav" );
		Server.PrecacheSound( "weapons/ric3.wav" );
		Server.PrecacheSound( "weapons/ric4.wav" );
		Server.PrecacheSound( "weapons/ric5.wav" );

		Server.LightStyle( 0, "m" );
		Server.LightStyle( 1, "mmnmmommommnonmmonqnmmo" );
		Server.LightStyle( 2, "abcdefghijklmnopqrstuvwxyzyxwvutsrqponmlkjihgfedcba" );
		Server.LightStyle( 3, "mmmmmaaaaammmmmaaaaaabcdefgabcdefg" );
		Server.LightStyle( 4, "mamamamamama" );
		Server.LightStyle( 5, "jklmnopqrstuvwxyzyxwvutsrqponmlkj" );
		Server.LightStyle( 6, "nmonqnmomnmomomno" );
		Server.LightStyle( 7, "mmmaaaabcdefgmmmmaaaammmaamm" );
		Server.LightStyle( 8, "mmmaaammmaaammmabcdefaaaammmmabcdefmmmaaaa" );
		Server.LightStyle( 9, "aaaaaaaazzzzzzzz" );
		Server.LightStyle( 10, "mmamammmmammamamaaamammma" );
		Server.LightStyle( 11, "abcdefghijklmnopqrrqponmlkjihgfedcba" );
		Server.LightStyle( 12, "mmnnmmnnnmmnn" );
		Server.LightStyle( 63, "a" );

		Decals.Init( Server );

		var graph = HalfLifeDll.WorldGraph;

		graph.InitGraph();

		if ( !graph.CheckNodFile( Server.Globals.MapName ) )
		{
			graph.AllocNodes();
		}
		else if ( !graph.LoadGraph( Server.Globals.MapName ) )
		{
			Util.Alert( AlertType.Console, "*Error opening .NOD file" );
			graph.AllocNodes();
		}
		else
		{
			Util.Alert( AlertType.Console, "\n*Graph Loaded!" );
		}

		Cvar.SetValue( "sv_zmax", Speed > 0f ? Speed : 4096f );

		if ( !string.IsNullOrEmpty( NetName ) )
			Util.Alert( AlertType.AiConsole, $"Chapter title: {NetName}" );

		if ( !string.IsNullOrEmpty( NetName ) && Create( "env_message", Vector3.Zero, Vector3.Zero, null ) is { } entity )
		{
			entity.SetThink( entity.CallUseToggle );
			entity.Message = NetName;
			NetName = null;
			entity.NextThink = (float)(Server.Globals.Time + 0.3);
			entity.SpawnFlags = EnvMessage.SpawnFlagOnce;
		}

		Cvar.SetValue( "v_dark", (SpawnFlags & SpawnFlagDark) != 0 ? 1f : 0f );
		HalfLifeDll.DisplayTitle = (SpawnFlags & SpawnFlagTitle) != 0;
		Cvar.SetValue( "mp_defaultteam", (SpawnFlags & SpawnFlagForceTeam) != 0 ? 1f : 0f );
	}
}

[LinkEntityToClass( "infodecal" )]
public class InfoDecal : BaseEntity
{
	private const int SpawnFlagNotInDeathmatch = 2048;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "texture" )
			return base.KeyValue( key, value );

		Skin = Server.DecalIndex( value );

		if ( Skin < 0 )
			Util.Alert( AlertType.Console, $"Can't find decal {value}" );

		return Skin >= 0;
	}

	public override void Spawn()
	{
		if ( Skin < 0 || (Server.Deathmatch && (SpawnFlags & SpawnFlagNotInDeathmatch) != 0) )
		{
			Server.FreeEdict( this );
			return;
		}

		if ( string.IsNullOrEmpty( TargetName ) )
		{
			SetThink( StaticDecal );
			NextThink = Server.Globals.Time;
		}
		else
		{
			SetThink( null );
			SetUse( TriggerDecal );
		}
	}

	private void TriggerDecal( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var trace = Server.TraceLine( Origin - new Vector3( 5f ), Origin + new Vector3( 5f ), IgnoreMonsters, this );
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( (int)TempEntity.BspDecal );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( Skin );

		var entityIndex = (short)trace.Ent.Index;

		Server.WriteShort( entityIndex );

		if ( entityIndex != 0 )
			Server.WriteShort( trace.Ent.ModelIndex );

		Server.MessageEnd();

		SetThink( SubRemove );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	private void StaticDecal()
	{
		var trace = Server.TraceLine( Origin - new Vector3( 5f ), Origin + new Vector3( 5f ), IgnoreMonsters, this );
		var entityIndex = trace.Ent.Index;

		Server.StaticDecal( Origin, Skin, entityIndex, entityIndex != 0 ? trace.Ent.ModelIndex : 0 );

		SubRemove();
	}
}

[LinkEntityToClass( "bodyque" )]
public class Corpse : BaseEntity
{
	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;
}
