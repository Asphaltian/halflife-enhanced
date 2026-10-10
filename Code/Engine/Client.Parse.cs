using System;
using System.IO;

namespace GoldSrc;

public sealed class PlayerInfo
{
	public const int MaxInfoString = 256;
	public const int MaxNameLength = 32;
	public const int MaxModelLength = 64;

	public int UserId;
	public string UserInfo = "";
	public string Name = "";
	public int Spectator;
	public int Ping;
	public int PacketLoss;
	public string Model = "";
	public int TopColor;
	public int BottomColor;
	public byte[] HashedCdKey = new byte[16];
	public long SteamId;
	public int RenderFrame;
	public int GaitSequence;
	public float GaitFrame;
	public float GaitYaw;
	public Vector3 PrevGaitOrigin;
	public List<Customization> Customizations = [];
}

public sealed class HudPlayerInfo
{
	public string Name;
	public short Ping;
	public bool ThisPlayer;
	public bool Spectator;
	public byte PacketLoss;
	public string Model;
	public short TopColor;
	public short BottomColor;
	public long SteamId;
}

partial class Client
{
	private static string[] ServerMessageNames =
	[
		"svc_bad", "svc_nop", "svc_disconnect", "svc_event", "svc_version", "svc_setview", "svc_sound", "svc_time", "svc_print",
		"svc_stufftext", "svc_setangle", "svc_serverinfo", "svc_lightstyle", "svc_updateuserinfo", "svc_deltadescription",
		"svc_clientdata", "svc_stopsound", "svc_pings", "svc_particle", "svc_damage", "svc_spawnstatic", "svc_event_reliable",
		"svc_spawnbaseline", "svc_temp_entity", "svc_setpause", "svc_signonnum", "svc_centerprint", "svc_killedmonster",
		"svc_foundsecret", "svc_spawnstaticsound", "svc_intermission", "svc_finale", "svc_cdtrack", "svc_restore", "svc_cutscene",
		"svc_weaponanim", "svc_decalname", "svc_roomtype", "svc_addangle", "svc_newusermsg", "svc_packetentities",
		"svc_deltapacketentities", "svc_choke", "svc_resourcelist", "svc_newmovevars", "svc_resourcerequest", "svc_customization",
		"svc_crosshairangle", "svc_soundfade", "svc_filetxferfailed", "svc_hltv", "svc_director", "svc_voiceinit", "svc_voicedata",
		"svc_sendextrainfo", "svc_timescale", "svc_resourcelocation", "svc_sendcvarvalue", "svc_sendcvarvalue2", "svc_exec"
	];

	private readonly MessageReader _netMessage = new();
	private readonly PlayerInfo[] _players = new PlayerInfo[Server.MaxClientsLimit];
	private readonly byte[] _userMessageData = new byte[Server.MaxUserMessageSize];

	private int _viewEntity;

	public int ViewEntityIndex => _viewEntity;

	public PlayerInfo GetPlayerInfo( int slot )
	{
		return slot >= 0 && slot < Server.MaxClientsLimit ? _players[slot] ??= new PlayerInfo() : null;
	}

	public void GetPlayerInfo( int index, HudPlayerInfo info )
	{
		if ( index > _maxClients || index < 1 || GetPlayerInfo( index - 1 ) is not { Name.Length: > 0 } player )
		{
			info.Name = null;
			info.ThisPlayer = false;

			return;
		}

		info.Name = player.Name;
		info.Ping = (short)player.Ping;
		info.Spectator = player.Spectator != 0;
		info.PacketLoss = (byte)player.PacketLoss;
		info.ThisPlayer = index - 1 == _playerNum;
		info.TopColor = (short)player.TopColor;
		info.BottomColor = (short)player.BottomColor;
		info.Model = player.Model;
		info.SteamId = player.SteamId;
	}

	private void ParseDirector( MessageReader message )
	{
		var data = new byte[message.ReadByte()];

		message.ReadBuf( data.Length, data );

		_clientDll.DirectorMessage( data );
	}

	private static Exception HostError( string message ) => new InvalidDataException( message );

	private void ParseServerMessage( ArraySegment<byte> packet )
	{
		if ( packet.Array is null )
			return;

		_netMessage.BeginReading( packet.Array, packet.Count );

		ParseServerMessage( _netMessage );
	}

	private void ParsePings( MessageReader message )
	{
		message.StartBitReading();

		while ( message.ReadBits( 1 ) != 0 )
		{
			var slot = (int)message.ReadBits( 5 );
			var ping = (int)message.ReadBits( 12 );
			var loss = (int)message.ReadBits( 7 );

			GetPlayerInfo( slot ).Ping = ping;
			GetPlayerInfo( slot ).PacketLoss = loss;
		}

		message.EndBitReading();
	}

	private const int MessageBuckets = 64;
	private const int UserMessageBucket = MessageBuckets - 1;

	private readonly int[] _messageCounts = new int[MessageBuckets];
	private readonly int[] _messageBytes = new int[MessageBuckets];
	private readonly int[] _packetMessageBytes = new int[MessageBuckets];
	private int _userMessageCount;

	private void ClearMessageStats()
	{
		Array.Clear( _messageCounts );
		Array.Clear( _messageBytes );
	}

	private void MessagesCommand()
	{
		var total = 0;

		Log.Info( "-------- Message Load ---------\n" );

		for ( var i = 0; i < UserMessageBucket; i++ )
		{
			if ( _messageCounts[i] == 0 )
				continue;

			var name = i < ServerMessageNames.Length ? ServerMessageNames[i] : "bogus #";

			Log.Info( $"{i}:{name}: {_messageCounts[i]} msgs:{_messageBytes[i] * (1f / 1024f):F2}K\n" );

			total += _messageCounts[i];
		}

		Log.Info( $"User messages:  {_userMessageCount}:{_messageBytes[UserMessageBucket] * (1f / 1024f):F2}K\n" );
		Log.Info( $"------ End:  {_userMessageCount + total} Total----\n" );
	}

	[ConCmd( "cl_messages" )]
	private static void MessagesConsoleCommand() => Current?.MessagesCommand();

	private void ParseServerMessage( MessageReader message )
	{
		var start = message.ReadCount;
		var bucket = -1;
		var commandStart = start;

		Array.Clear( _packetMessageBytes );

		var command = -1;

		while ( true )
		{
			if ( bucket >= 0 )
			{
				var bytes = message.ReadCount - commandStart;
				var frame = GetFrame( _parseCountMod );

				_packetMessageBytes[bucket] += bytes;

				switch ( command )
				{
					case Server.PacketEntitiesMessage:
					case Server.DeltaPacketEntitiesMessage:
						var playerBytes = _playerEntityBits / 8;

						frame.PacketEntityBytes += (ushort)(bytes - playerBytes);
						frame.PlayerInfoBytes += (ushort)playerBytes;
						break;

					case Server.TempEntityMessage:
						frame.TentityBytes += (ushort)bytes;
						break;

					case Server.SoundMessage:
						frame.SoundBytes += (ushort)bytes;
						break;

					case Server.ClientDataMessage:
						frame.ClientBytes += (ushort)bytes;
						break;

					case Server.EventMessage:
					case Server.EventReliableMessage:
						frame.EventBytes += (ushort)bytes;
						break;

					default:
						if ( bucket == UserMessageBucket )
							frame.UsrBytes += (ushort)bytes;

						break;
				}
			}

			if ( message.BadRead )
				throw HostError( "CL_ParseServerMessage: Bad server message" );

			commandStart = message.ReadCount;

			command = message.ReadByte();

			if ( command == -1 )
			{
				GetFrame( _parseCountMod ).MsgBytes += (ushort)message.Size;

				for ( var i = 0; i < MessageBuckets; i++ )
					_messageBytes[i] += _packetMessageBytes[i];

				WriteDemoMessages( start, message, _state );
				return;
			}

			if ( command < ServerMessageNames.Length )
			{
				bucket = command;

				_messageCounts[command]++;
			}
			else
			{
				bucket = UserMessageBucket;

				_userMessageCount++;
			}

			switch ( command )
			{
				case 0:
					throw HostError( $"CL_ParseServerMessage: Illegible server message - {ServerMessageNames[0]}\n" );

				case 1:
					break;

				case Server.DisconnectMessage:
					ParseDisconnect( message );
					break;

				case Server.EventMessage:
					ParseEvent( message );
					break;

				case Server.EventReliableMessage:
					ParseEventReliable( message );
					break;

				case Server.SoundMessage:
					ParseSound( message );
					break;

				case Server.SpawnStaticSoundMessage:
					ParseStaticSound( message );
					break;

				case Server.SetViewMessage:
					_viewEntity = message.ReadShort();
					break;

				case Server.PrintMessage:
					ConsolePrint( message.ReadString() );
					break;

				case Server.CenterPrintMessage:
					CenterPrint( message.ReadString() );
					break;

				case Server.StuffTextMessage:
					ParseStuffText( message );
					break;

				case Server.ServerInfoMessage:
					ParseServerInfo( message );
					break;

				case Server.LightStyleMessage:
					ParseLightStyle( message );
					break;

				case Server.DeltaDescriptionMessage:
					ParseDeltaDescription( message );
					break;

				case Server.SignOnNumMessage:
					ParseSignonNum( message );
					break;

				case Server.RestoreMessage:
					ParseRestore( message );
					break;

				case Server.NewUserMsgMessage:
					ParseNewUserMsg( message );
					break;

				case Server.ResourceListMessage:
					ParseResourceList( message );
					break;

				case Server.ResourceRequestMessage:
					SendResourceListBlock( message );
					break;

				case Server.CustomizationMessage:
					ParseCustomization( message );
					break;

				case Server.FileTransferFailedMessage:
					ParseFileTransferFailed( message );
					break;

				case Server.VoiceInitMessage:
					message.ReadString();
					message.ReadByte();
					break;

				case Server.SendExtraInfoMessage:
					ParseSendExtraInfo( message );
					break;

				case Server.ResourceLocationMessage:
					ParseResourceLocation( message );
					break;

				case Server.NewMoveVarsMessage:
					ParseNewMoveVars( message );
					break;

				case Server.TimeMessage:
					ParseTime( message );
					break;

				case Server.SetAngleMessage:
					ViewAngles = new Vector3( message.ReadHiresAngle(), message.ReadHiresAngle(), message.ReadHiresAngle() );
					break;

				case Server.AddAngleMessage:
					ViewAngles.y += message.ReadHiresAngle();
					break;

				case Server.ClientDataMessage:
					ParseClientData( message );
					break;

				case Server.ChokeMessage:
					GetFrame( _netchan.IncomingSequence ).Choked = true;
					break;

				case Server.PingsMessage:
					ParsePings( message );
					break;

				case Server.UpdateUserInfoMessage:
					ParseUpdateUserInfo( message );
					break;

				case Server.ParticleMessage:
					ParseParticle( message );
					break;

				case Server.TempEntityMessage:
					ParseTempEntity( message );
					break;

				case Server.SpawnBaselineMessage:
					ParseBaseline( message );
					break;

				case Server.PacketEntitiesMessage:
					ParsePacketEntities( message, false );
					SetSolidEntities();
					break;

				case Server.DeltaPacketEntitiesMessage:
					ParsePacketEntities( message, true );
					SetSolidEntities();
					break;

				case Server.SetPauseMessage:
					_paused = message.ReadByte() != 0;

					if ( _paused )
						CdAudio.Pause();
					else
						CdAudio.Resume();

					break;

				case Server.IntermissionMessage:
					_intermission = 1;
					break;

				case Server.CdTrackMessage:
				{
					var track = message.ReadByte();

					message.ReadByte();

					CdAudio.Play( (_demoPlayback || _demoRecording) && _forceTrack != -1 ? _forceTrack : track, true );
					break;
				}

				case Server.WeaponAnimMessage:
				{
					var sequence = message.ReadByte();
					var body = message.ReadByte();

					_viewModel.WeaponAnim( sequence, body );
					_weaponAnimReceived = true;
					break;
				}

				case Server.RoomTypeMessage:
					Cvar.SetValue( "room_type", message.ReadShort() );
					break;

				case Server.CrosshairAngleMessage:
				{
					var pitch = (float)(message.ReadChar() * 0.2);
					var yaw = (float)(message.ReadChar() * 0.2);

					_crosshairAngle = new Vector3( pitch, yaw, 0f );
					break;
				}

				case Server.HltvMessage:
					ParseHltv( message );
					break;

				case Server.DirectorMessage:
					ParseDirector( message );
					break;

				default:
					if ( command < ServerMessageNames.Length )
						throw new NotSupportedException( $"{ServerMessageNames[command]} is not ported" );

					DispatchUserMessage( message, command );
					break;
			}
		}
	}

	private const int MaxSkyNameLength = 31;

	[ConVar( "gl_zmax" )]
	private static float ZMax { get; set; } = 4096f;

	[ConVar( "gl_wateramp", ConVarFlags.Saved )]
	private static float WaterAmp { get; set; } = 0.3f;

	private readonly MoveVars _moveVars = new();

	private void ParseNewMoveVars( MessageReader message )
	{
		_moveVars.Gravity = message.ReadFloat();
		_moveVars.StopSpeed = message.ReadFloat();
		_moveVars.MaxSpeed = message.ReadFloat();
		_moveVars.SpectatorMaxSpeed = message.ReadFloat();
		_moveVars.Accelerate = message.ReadFloat();
		_moveVars.AirAccelerate = message.ReadFloat();
		_moveVars.WaterAccelerate = message.ReadFloat();
		_moveVars.Friction = message.ReadFloat();
		_moveVars.EdgeFriction = message.ReadFloat();
		_moveVars.WaterFriction = message.ReadFloat();
		_moveVars.EntGravity = message.ReadFloat();
		_moveVars.Bounce = message.ReadFloat();
		_moveVars.StepSize = message.ReadFloat();
		_moveVars.MaxVelocity = message.ReadFloat();
		_moveVars.ZMax = message.ReadFloat();
		_moveVars.WaveHeight = message.ReadFloat();
		_moveVars.Footsteps = message.ReadByte();
		_moveVars.RollAngle = message.ReadFloat();
		_moveVars.RollSpeed = message.ReadFloat();
		_moveVars.SkyColorR = message.ReadFloat();
		_moveVars.SkyColorG = message.ReadFloat();
		_moveVars.SkyColorB = message.ReadFloat();
		_moveVars.SkyVecX = message.ReadFloat();
		_moveVars.SkyVecY = message.ReadFloat();
		_moveVars.SkyVecZ = message.ReadFloat();

		var skyName = message.ReadString();

		_moveVars.SkyName = skyName.Length > MaxSkyNameLength ? skyName[..MaxSkyNameLength] : skyName;

		if ( _moveVars.ZMax != ZMax )
			Cvar.SetValue( "gl_zmax", _moveVars.ZMax );

		if ( _moveVars.WaveHeight != WaterAmp )
			Cvar.SetValue( "gl_wateramp", _moveVars.WaveHeight );

		if ( GetEntity( 0 ) is { } world )
			world.CurState.Scale = WaterAmp;
	}

	private Sfx FindSound( int flags, int index )
	{
		if ( (flags & Server.SoundSentence) == 0 )
			return _soundPrecache[index];

		return new Sfx { Name = index < Server.MaxSentenceNumber ? $"!{Sound.SentenceName( index )}" : $"!#{index - Server.MaxSentenceNumber}" };
	}

	private void ParseSound( MessageReader message )
	{
		message.StartBitReading();

		var flags = (int)message.ReadBits( 9 );
		var volume = (flags & Server.SoundVolume) != 0 ? (float)((float)message.ReadBits( 8 ) / 255.0) : 1f;
		var attenuation = (flags & Server.SoundAttenuation) != 0 ? (float)((float)message.ReadBits( 8 ) * 0.015625) : 1f;
		var channel = (int)message.ReadBits( 3 );
		var entity = (int)message.ReadBits( 11 );
		var index = (int)message.ReadBits( (flags & Server.SoundLargeIndex) != 0 ? 16 : 8 );
		var origin = message.ReadBitVec3Coord( Vector3.Zero );
		var pitch = (flags & Server.SoundPitch) != 0 ? (int)message.ReadBits( 8 ) : Server.PitchNormal;

		message.EndBitReading();

		if ( entity >= _maxEdicts )
		{
			Log.Trace( $"CL_Parse_Sound: ent = {entity}, cl.max_edicts {_maxEdicts}\n" );
			return;
		}

		var sfx = FindSound( flags, index );

		if ( channel == (int)SoundChannel.Static )
			Sound.StartStaticSound( entity, (int)SoundChannel.Static, sfx, origin, volume, attenuation, flags, pitch );
		else
			Sound.StartDynamicSound( entity, channel, sfx, origin, volume, attenuation, flags, pitch );
	}

	private void ParseStaticSound( MessageReader message )
	{
		var origin = message.ReadPosition();
		var index = message.ReadShort();
		var volume = (float)(message.ReadByte() / 255.0);
		var attenuation = (float)(message.ReadByte() * 0.015625);
		var entity = message.ReadShort();
		var pitch = message.ReadByte();
		var flags = message.ReadByte();

		Sound.StartStaticSound( entity, (int)SoundChannel.Static, FindSound( flags, index ), origin, volume, attenuation, flags, pitch );
	}

	private void DispatchUserMessage( MessageReader message, int id )
	{
		if ( id >= 256 )
			throw HostError( $"DispatchUserMsg:  Illegal User Msg {id}\n" );

		if ( _userMessages.Find( entry => entry.Id == id ) is not { } userMessage )
			throw HostError( $"UserMsg: Not Present on Client {id}\n" );

		var size = userMessage.Size;

		if ( size == -1 )
			size = message.ReadByte();

		if ( size > Server.MaxUserMessageSize )
			throw HostError( $"DispatchUserMsg:  User Msg {userMessage.Name}/{userMessage.Id} sent too much data ({size} bytes), {Server.MaxUserMessageSize} bytes max.\n" );

		message.ReadBuf( size, _userMessageData );

		_clientDll.DispatchUserMsg( new UserMessage( userMessage.Id, userMessage.Size, userMessage.Name ), _userMessageData.AsSpan( 0, Math.Max( size, 0 ) ).ToArray() );
	}

	private static void ParseDisconnect( MessageReader message )
	{
		message.ReadString();

		Log.Info( "Server disconnected\n" );
	}

	private void ParseUpdateUserInfo( MessageReader message )
	{
		var slot = message.ReadByte();

		if ( slot >= Server.MaxClientsLimit )
			throw HostError( "CL_ParseServerMessage: svc_updateuserinfo > MAX_CLIENTS" );

		var player = GetPlayerInfo( slot );

		player.UserId = message.ReadLong();

		var info = message.ReadString();

		player.UserInfo = info.Length >= PlayerInfo.MaxInfoString ? info[..(PlayerInfo.MaxInfoString - 1)] : info;

		message.ReadBuf( 16, player.HashedCdKey );

		var name = Info.ValueForKey( player.UserInfo, "name" );
		var model = Info.ValueForKey( player.UserInfo, "model" );

		player.Name = name.Length >= PlayerInfo.MaxNameLength ? name[..(PlayerInfo.MaxNameLength - 1)] : name;
		player.Model = model.Length >= PlayerInfo.MaxModelLength ? model[..(PlayerInfo.MaxModelLength - 1)] : model;
		player.TopColor = Common.Atoi( Info.ValueForKey( player.UserInfo, "topcolor" ) );
		player.BottomColor = Common.Atoi( Info.ValueForKey( player.UserInfo, "bottomcolor" ) );
		player.Spectator = Common.Atoi( Info.ValueForKey( player.UserInfo, "*hltv" ) );
		player.SteamId = long.TryParse( Info.ValueForKey( player.UserInfo, "*sid" ), out var steamId ) ? steamId : 0;

		if ( player.UserInfo.Length == 0 || player.Name.Length == 0 )
			ClearCustomizationList( player.Customizations, true );
	}
}
