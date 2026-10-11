using System;
using Sandbox.Mounting;

namespace GoldSrc;

partial class Server
{
	private static readonly string[] UnloggedCvars = ["mp_fragsleft", "mp_timeleft"];
	private static readonly string[] ProtectedCvars = ["sv_password", "max_queries_sec", "max_queries_sec_global", "max_queries_window"];

	public static bool IsProtectedCvar( string name ) => Array.IndexOf( ProtectedCvars, name ) >= 0;

	public static void SetServerCvar( string name, ref float field, float value )
	{
		var changed = field != value;

		field = value;

		if ( changed )
			ServerCvarChanged( name, Cvar.FormatValue( value ) );
	}

	public static void SetServerCvar( string name, ref string field, string value )
	{
		var changed = field != value;

		field = value;

		if ( changed )
			ServerCvarChanged( name, value );
	}

	private static void ServerCvarChanged( string name, string value )
	{
		if ( Array.IndexOf( UnloggedCvars, name ) >= 0 )
			return;

		if ( Array.IndexOf( ProtectedCvars, name ) >= 0 )
			value = "***PROTECTED***";

		LogPrintf( $"Server cvar \"{name}\" = \"{value}\"\n" );

		Current?.BroadcastPrintf( $"\"{name}\" changed to \"{value}\"\n" );
	}

	[ConVar( "mp_consistency", ConVarFlags.Replicated )]
	private static float MpConsistency { get; set => SetServerCvar( "mp_consistency", ref field, value ); } = 1f;

	[ConVar( "sv_contact", ConVarFlags.Replicated )]
	private static string SvContact { get; set => SetServerCvar( "sv_contact", ref field, value ); } = "";

	[ConVar( "sv_logblocks", ConVarFlags.Replicated )]
	private static float SvLogBlocks { get; set => SetServerCvar( "sv_logblocks", ref field, value ); }

	[ConVar( "sv_uploadmax", ConVarFlags.Replicated )]
	private static float SvUploadMax { get; set => SetServerCvar( "sv_uploadmax", ref field, value ); } = 0.5f;

	[ConVar( "sv_uploadmaxcount", ConVarFlags.Replicated )]
	private static float SvUploadMaxCount { get; set => SetServerCvar( "sv_uploadmaxcount", ref field, value ); } = 10f;

	[ConVar( "max_queries_sec" )]
	private static float MaxQueriesSec { get; set => SetServerCvar( "max_queries_sec", ref field, value ); } = 3f;

	[ConVar( "max_queries_sec_global" )]
	private static float MaxQueriesSecGlobal { get; set => SetServerCvar( "max_queries_sec_global", ref field, value ); } = 30f;

	[ConVar( "max_queries_window" )]
	private static float MaxQueriesWindow { get; set => SetServerCvar( "max_queries_window", ref field, value ); } = 60f;

	[ConVar( "sv_gravity", ConVarFlags.Replicated )]
	private static float SvGravity { get; set => SetServerCvar( "sv_gravity", ref field, value ); } = 800f;

	[ConVar( "sv_stopspeed", ConVarFlags.Replicated )]
	private static float SvStopSpeed { get; set => Server.SetServerCvar( "sv_stopspeed", ref field, value ); } = 100f;

	[ConVar( "sv_maxspeed", ConVarFlags.Replicated )]
	private static float SvMaxSpeed { get; set => Server.SetServerCvar( "sv_maxspeed", ref field, value ); } = 320f;

	[ConVar( "sv_spectatormaxspeed" )]
	private static float SvSpectatorMaxSpeed { get; set; } = 500f;

	[ConVar( "sv_accelerate", ConVarFlags.Replicated )]
	private static float SvAccelerate { get; set => Server.SetServerCvar( "sv_accelerate", ref field, value ); } = 10f;

	[ConVar( "sv_airaccelerate", ConVarFlags.Replicated )]
	private static float SvAirAccelerate { get; set => Server.SetServerCvar( "sv_airaccelerate", ref field, value ); } = 10f;

	[ConVar( "sv_wateraccelerate", ConVarFlags.Replicated )]
	private static float SvWaterAccelerate { get; set => Server.SetServerCvar( "sv_wateraccelerate", ref field, value ); } = 10f;

	[ConVar( "sv_friction", ConVarFlags.Replicated )]
	private static float SvFriction { get; set => Server.SetServerCvar( "sv_friction", ref field, value ); } = 4f;

	[ConVar( "edgefriction", ConVarFlags.Replicated )]
	private static float SvEdgeFriction { get; set => Server.SetServerCvar( "edgefriction", ref field, value ); } = 2f;

	[ConVar( "sv_waterfriction", ConVarFlags.Replicated )]
	private static float SvWaterFriction { get; set => Server.SetServerCvar( "sv_waterfriction", ref field, value ); } = 1f;

	[ConVar( "sv_bounce", ConVarFlags.Replicated )]
	private static float SvBounce { get; set => Server.SetServerCvar( "sv_bounce", ref field, value ); } = 1f;

	[ConVar( "sv_stepsize", ConVarFlags.Replicated )]
	private static float SvStepSize { get; set => Server.SetServerCvar( "sv_stepsize", ref field, value ); } = 18f;

	[ConVar( "lservercfgfile" )]
	private static string ListenServerConfigFile { get; set; } = "listenserver.cfg";

	[ConVar( "motdfile" )]
	private static string MotdFile { get; set; } = "motd.txt";

	[ConVar( "mp_footsteps", ConVarFlags.Replicated )]
	private static float SvFootsteps { get; set => Server.SetServerCvar( "mp_footsteps", ref field, value ); } = 1f;

	[ConVar( "sv_maxvelocity" )]
	private static float SvMaxVelocity { get; set; } = 2000f;

	[ConVar( "sv_zmax" )]
	private static float SvZMax { get; set; } = 4096f;

	[ConVar( "sv_wateramp" )]
	private static float SvWaterAmp { get; set; } = 0f;

	[ConVar( "sv_rollangle" )]
	private static float SvRollAngle { get; set; } = 2f;

	[ConVar( "sv_rollspeed" )]
	private static float SvRollSpeed { get; set; } = 200f;

	[ConVar( "sv_skycolor_r" )]
	private static float SvSkyColorR { get; set; } = 0f;

	[ConVar( "sv_skycolor_g" )]
	private static float SvSkyColorG { get; set; } = 0f;

	[ConVar( "sv_skycolor_b" )]
	private static float SvSkyColorB { get; set; } = 0f;

	[ConVar( "sv_skyvec_x" )]
	private static float SvSkyVecX { get; set; } = 0f;

	[ConVar( "sv_skyvec_y" )]
	private static float SvSkyVecY { get; set; } = 0f;

	[ConVar( "sv_skyvec_z" )]
	private static float SvSkyVecZ { get; set; } = 0f;

	[ConVar( "sv_skyname" )]
	private static string SvSkyName { get; set; } = "desert";

	[ConVar( "skill" )]
	private static float Skill { get; set; } = 1f;

	[ConVar( "deathmatch", ConVarFlags.Replicated )]
	private static float DeathmatchCvar { get; set => Server.SetServerCvar( "deathmatch", ref field, value ); } = 0f;

	[ConVar( "coop", ConVarFlags.Replicated )]
	private static float CoopCvar { get; set => Server.SetServerCvar( "coop", ref field, value ); } = 0f;

	[ConVar( "mapchangecfgfile" )]
	private static string MapChangeConfigFile { get; set; } = "";

	[ConVar( "disconcfgfile" )]
	private static string DisconnectConfigFile { get; set; } = "";

	[ConVar( "sv_newunit" )]
	private static float SvNewUnit { get; set; } = 0f;

	[ConVar( "sv_allow_autoaim", ConVarFlags.Replicated )]
	private static float SvAllowAutoaim { get; set => Server.SetServerCvar( "sv_allow_autoaim", ref field, value ); } = 0f;

	[ConVar( "sv_allow_shaders", ConVarFlags.Replicated )]
	private static float SvAllowShaders { get; set => Server.SetServerCvar( "sv_allow_shaders", ref field, value ); } = 0f;

	[ConVar( "sv_aim", ConVarFlags.Saved )]
	private static float SvAim { get; set; } = 1f;

	[ConVar( "pausable", ConVarFlags.Replicated )]
	private static float Pausable { get; set => Server.SetServerCvar( "pausable", ref field, value ); } = 1f;

	[ConVar( "room_type" )]
	private static float RoomType { get; set; } = 0f;

	[ConVar( "v_dark" )]
	private static float ViewDark { get; set; } = 0f;

	[ConVar( "violence_hblood", ConVarFlags.Saved )]
	private static float ViolenceHumanBlood { get; set; } = 1f;

	[ConVar( "violence_ablood", ConVarFlags.Saved )]
	private static float ViolenceAlienBlood { get; set; } = 1f;

	[ConVar( "violence_hgibs", ConVarFlags.Saved )]
	private static float ViolenceHumanGibs { get; set; } = 1f;

	[ConVar( "violence_agibs", ConVarFlags.Saved )]
	private static float ViolenceAlienGibs { get; set; } = 1f;

	[ConVar( "_sv_override_scientist_mdl" )]
	private static string OverrideScientistModel { get; set; } = "";

	public MoveVars MoveVars { get; } = new();

	public void ServerCommand( string command )
	{
		if ( command.EndsWith( '\n' ) || command.EndsWith( ';' ) )
			Cmd.AddText( command );
		else
			Con.Printf( $"Error, bad server command {command}\n" );
	}

	public void EndSection( string section )
	{
		if ( string.IsNullOrEmpty( section ) )
			Con.Printf( " endsection with no arguments\n" );
		else if ( !section.Equals( "_oem_end_training", StringComparison.OrdinalIgnoreCase ) && !section.Equals( "_oem_end_logo", StringComparison.OrdinalIgnoreCase ) && !section.Equals( "_oem_end_demo", StringComparison.OrdinalIgnoreCase ) )
			Con.DPrintf( " endsection with unknown Section keyvalue\n" );

		Cmd.AddText( "\ndisconnect\n" );
	}

	public void TogglePause()
	{
		if ( Pausable == 0f )
		{
			ClientPrintf( "Pause not allowed.\n" );
			return;
		}

		Paused = !Paused;

		BroadcastPrintf( Paused ? $"{_hostClient.Name} paused the game\n" : $"{_hostClient.Name} unpaused the game\n" );

		_reliableDatagram.WriteByte( SetPauseMessage );
		_reliableDatagram.WriteByte( Paused ? 1 : 0 );
	}

	public void SetPause( bool paused )
	{
		if ( Pausable == 0f )
			return;

		Paused = paused;

		_reliableDatagram.WriteByte( SetPauseMessage );
		_reliableDatagram.WriteByte( Paused ? 1 : 0 );
	}

	public void ServerExecute() => Cmd.Execute();

	public void ClientCommand( Edict client, string command ) => Cmd.AddText( command );

	public static string LoadMountText( string fileName )
	{
		var task = FindMountPath( fileName ) is { } path ? MountUtility.FindLoader( path )?.GetOrCreate() : null;

		return task is { IsCompletedSuccessfully: true } ? task.Result as string : null;
	}

	public const int NewMoveVarsMessage = 44;

	private const int MaxSkyNameLength = 31;

	private bool MoveVarsChanged()
	{
		return SvMaxSpeed != MoveVars.MaxSpeed
			|| SvGravity != MoveVars.Gravity
			|| SvStopSpeed != MoveVars.StopSpeed
			|| SvSpectatorMaxSpeed != MoveVars.SpectatorMaxSpeed
			|| SvAccelerate != MoveVars.Accelerate
			|| SvAirAccelerate != MoveVars.AirAccelerate
			|| SvWaterAccelerate != MoveVars.WaterAccelerate
			|| SvFriction != MoveVars.Friction
			|| SvEdgeFriction != MoveVars.EdgeFriction
			|| SvWaterFriction != MoveVars.WaterFriction
			|| MoveVars.EntGravity != 1.0
			|| SvBounce != MoveVars.Bounce
			|| SvStepSize != MoveVars.StepSize
			|| SvMaxVelocity != MoveVars.MaxVelocity
			|| SvZMax != MoveVars.ZMax
			|| SvWaterAmp != MoveVars.WaveHeight
			|| SvFootsteps != MoveVars.Footsteps
			|| SvRollAngle != MoveVars.RollAngle
			|| SvRollSpeed != MoveVars.RollSpeed
			|| SvSkyColorR != MoveVars.SkyColorR
			|| SvSkyColorG != MoveVars.SkyColorG
			|| SvSkyColorB != MoveVars.SkyColorB
			|| SvSkyVecX != MoveVars.SkyVecX
			|| SvSkyVecY != MoveVars.SkyVecY
			|| SvSkyVecZ != MoveVars.SkyVecZ
			|| SvSkyName != MoveVars.SkyName;
	}

	private void WriteMoveVars( SizeBuf message )
	{
		message.WriteByte( NewMoveVarsMessage );
		message.WriteFloat( MoveVars.Gravity );
		message.WriteFloat( MoveVars.StopSpeed );
		message.WriteFloat( MoveVars.MaxSpeed );
		message.WriteFloat( MoveVars.SpectatorMaxSpeed );
		message.WriteFloat( MoveVars.Accelerate );
		message.WriteFloat( MoveVars.AirAccelerate );
		message.WriteFloat( MoveVars.WaterAccelerate );
		message.WriteFloat( MoveVars.Friction );
		message.WriteFloat( MoveVars.EdgeFriction );
		message.WriteFloat( MoveVars.WaterFriction );
		message.WriteFloat( MoveVars.EntGravity );
		message.WriteFloat( MoveVars.Bounce );
		message.WriteFloat( MoveVars.StepSize );
		message.WriteFloat( MoveVars.MaxVelocity );
		message.WriteFloat( MoveVars.ZMax );
		message.WriteFloat( MoveVars.WaveHeight );
		message.WriteByte( MoveVars.Footsteps != 0 ? 1 : 0 );
		message.WriteFloat( MoveVars.RollAngle );
		message.WriteFloat( MoveVars.RollSpeed );
		message.WriteFloat( MoveVars.SkyColorR );
		message.WriteFloat( MoveVars.SkyColorG );
		message.WriteFloat( MoveVars.SkyColorB );
		message.WriteFloat( MoveVars.SkyVecX );
		message.WriteFloat( MoveVars.SkyVecY );
		message.WriteFloat( MoveVars.SkyVecZ );
		message.WriteString( MoveVars.SkyName );
	}

	private void CheckMoveVars()
	{
		if ( !MoveVarsChanged() )
			return;

		SetMoveVars();

		foreach ( var client in Clients )
		{
			if ( !client.FakeClient && (client.Active || client.Spawned || client.Connected) )
				WriteMoveVars( client.Message );
		}
	}

	private void SetMoveVars()
	{
		MoveVars.Gravity = SvGravity;
		MoveVars.StopSpeed = SvStopSpeed;
		MoveVars.MaxSpeed = SvMaxSpeed;
		MoveVars.SpectatorMaxSpeed = SvSpectatorMaxSpeed;
		MoveVars.Accelerate = SvAccelerate;
		MoveVars.AirAccelerate = SvAirAccelerate;
		MoveVars.WaterAccelerate = SvWaterAccelerate;
		MoveVars.Friction = SvFriction;
		MoveVars.EdgeFriction = SvEdgeFriction;
		MoveVars.WaterFriction = SvWaterFriction;
		MoveVars.EntGravity = 1f;
		MoveVars.Bounce = SvBounce;
		MoveVars.StepSize = SvStepSize;
		MoveVars.MaxVelocity = SvMaxVelocity;
		MoveVars.ZMax = SvZMax;
		MoveVars.WaveHeight = SvWaterAmp;
		MoveVars.Footsteps = (int)SvFootsteps;
		MoveVars.RollAngle = SvRollAngle;
		MoveVars.RollSpeed = SvRollSpeed;
		MoveVars.SkyColorR = SvSkyColorR;
		MoveVars.SkyColorG = SvSkyColorG;
		MoveVars.SkyColorB = SvSkyColorB;
		MoveVars.SkyVecX = SvSkyVecX;
		MoveVars.SkyVecY = SvSkyVecY;
		MoveVars.SkyVecZ = SvSkyVecZ;
		MoveVars.SkyName = SvSkyName.Length > MaxSkyNameLength ? SvSkyName[..MaxSkyNameLength] : SvSkyName;
	}
}
