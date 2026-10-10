using System;
using System.Globalization;
using System.Text;

namespace GoldSrc;

public sealed class DemoFile
{
	private byte[] _data;

	public string Name { get; }

	public int Length { get; private set; }

	public int Position { get; set; }

	public DemoFile( string name )
	{
		Name = name;
		_data = new byte[0x4000];
	}

	public DemoFile( string name, byte[] data )
	{
		Name = name;
		_data = data;
		Length = data.Length;
	}

	public int Read( Span<byte> buffer )
	{
		var count = Math.Clamp( Length - Position, 0, buffer.Length );

		_data.AsSpan( Position, count ).CopyTo( buffer );

		Position += count;

		return count;
	}

	public void Write( ReadOnlySpan<byte> buffer )
	{
		var end = Position + buffer.Length;

		if ( end > _data.Length )
			Array.Resize( ref _data, Math.Max( end, _data.Length * 2 ) );

		buffer.CopyTo( _data.AsSpan( Position ) );

		Position = end;
		Length = Math.Max( Length, end );
	}

	public int ReadByte()
	{
		Span<byte> value = stackalloc byte[1];

		Read( value );

		return value[0];
	}

	public int ReadInt()
	{
		Span<byte> value = stackalloc byte[4];

		Read( value );

		return BitConverter.ToInt32( value );
	}

	public float ReadFloat() => BitConverter.Int32BitsToSingle( ReadInt() );

	public void WriteByte( int value )
	{
		Span<byte> image = [(byte)value];

		Write( image );
	}

	public void WriteInt( int value )
	{
		Span<byte> image = stackalloc byte[4];

		BitConverter.TryWriteBytes( image, value );

		Write( image );
	}

	public void WriteFloat( float value ) => WriteInt( BitConverter.SingleToInt32Bits( value ) );

	public void SeekEnd() => Position = Length;

	public byte[] ToArray() => _data.AsSpan( 0, Length ).ToArray();
}

public sealed class DemoEntry
{
	public const int Size = 0x5c;
	public const int DescriptionSize = 0x40;

	public const int FlagTitle = 1;
	public const int FlagPlay = 4;
	public const int FlagFadeInSlow = 8;
	public const int FlagFadeInFast = 0x10;
	public const int FlagFadeOutSlow = 0x20;
	public const int FlagFadeOutFast = 0x40;

	public int Type;
	public string Description = "";
	public int Flags;
	public int CdTrack;
	public float TrackTime;
	public int Frames;
	public int Offset;
	public int Length;

	public static DemoEntry Read( byte[] image, int offset )
	{
		return new DemoEntry
		{
			Type = StructImage.ReadInt( image, offset ),
			Description = StructImage.ReadString( image, offset + 4, DescriptionSize ),
			Flags = StructImage.ReadInt( image, offset + 0x44 ),
			CdTrack = StructImage.ReadInt( image, offset + 0x48 ),
			TrackTime = StructImage.ReadFloat( image, offset + 0x4c ),
			Frames = StructImage.ReadInt( image, offset + 0x50 ),
			Offset = StructImage.ReadInt( image, offset + 0x54 ),
			Length = StructImage.ReadInt( image, offset + 0x58 )
		};
	}

	public void Write( byte[] image, int offset )
	{
		StructImage.Write( image, offset, Type );
		StructImage.Write( image, offset + 4, DescriptionSize, Description );
		StructImage.Write( image, offset + 0x44, Flags );
		StructImage.Write( image, offset + 0x48, CdTrack );
		StructImage.Write( image, offset + 0x4c, TrackTime );
		StructImage.Write( image, offset + 0x50, Frames );
		StructImage.Write( image, offset + 0x54, Offset );
		StructImage.Write( image, offset + 0x58, Length );
	}

	public DemoEntry Clone()
	{
		return new DemoEntry
		{
			Type = Type,
			Description = Description,
			Flags = Flags,
			CdTrack = CdTrack,
			TrackTime = TrackTime,
			Frames = Frames,
			Offset = Offset,
			Length = Length
		};
	}
}

public sealed class DemoHeader
{
	public const int Size = 0x220;
	public const string FileStamp = "HLDEMO";

	private const int NameSize = 0x104;

	public string Stamp = "";
	public int DemoProtocol;
	public int NetProtocol;
	public string MapName = "";
	public string GameDirectory = "";
	public int MapCrc;
	public int DirectoryOffset;

	public bool IsDemo => Stamp.StartsWith( FileStamp, StringComparison.Ordinal );

	public static DemoHeader Read( DemoFile file )
	{
		var image = new byte[Size];

		file.Read( image );

		return new DemoHeader
		{
			Stamp = StructImage.ReadString( image, 0, 8 ),
			DemoProtocol = StructImage.ReadInt( image, 8 ),
			NetProtocol = StructImage.ReadInt( image, 0xc ),
			MapName = StructImage.ReadString( image, 0x10, NameSize ),
			GameDirectory = StructImage.ReadString( image, 0x114, NameSize ),
			MapCrc = StructImage.ReadInt( image, 0x218 ),
			DirectoryOffset = StructImage.ReadInt( image, 0x21c )
		};
	}

	public void Write( DemoFile file )
	{
		var image = new byte[Size];

		StructImage.Write( image, 0, 8, Stamp );
		StructImage.Write( image, 8, DemoProtocol );
		StructImage.Write( image, 0xc, NetProtocol );
		StructImage.Write( image, 0x10, NameSize, MapName );
		StructImage.Write( image, 0x114, NameSize, GameDirectory );
		StructImage.Write( image, 0x218, MapCrc );
		StructImage.Write( image, 0x21c, DirectoryOffset );

		file.Write( image );
	}
}

partial class Client
{
	private const int DemoProtocol = 5;
	private const int DemoInfoSize = 0x1b4;
	private const int DemoRefParamsOffset = 4;
	private const int DemoRefParamsSize = 0xe8;
	private const int DemoCmdOffset = 0xec;
	private const int DemoMoveVarsOffset = 0x120;
	private const int DemoMoveVarsSize = 0x84;
	private const int DemoViewOffset = 0x1a4;
	private const int DemoViewModelOffset = 0x1b0;
	private const int DemoFrameHistory = 32;
	private const int DemoInterpolationFrames = 0x1f;
	private const int MaxDemoEntries = 0x400;
	private const int MaxDemoMessage = 0x10000;
	private const int MaxDemoBuffer = 0x8000;
	private const int DemoCommandSize = 0x40;
	private const int DemoClientDataSize = 0x20;
	private const int MaxDemos = 32;
	private const int MaxDemoName = 15;
	private const int MaxGameGaugeSeconds = 300;
	private const double DemoInterpolationDelay = 0.02;
	private const float MinDemoTimeScale = 0.001f;

	private const int DemoNetworkStartup = 0;
	private const int DemoNetwork = 1;
	private const int DemoStart = 2;
	private const int DemoConsoleCommand = 3;
	private const int DemoClientData = 4;
	private const int DemoNextSection = 5;
	private const int DemoEvent = 6;
	private const int DemoWeaponAnim = 7;
	private const int DemoSound = 8;
	private const int DemoBuffer = 9;

	[ConVar( "cl_gg" )]
	private static float GameGauge { get; set; }

	private readonly byte[] _demoInfo = new byte[DemoInfoSize];
	private readonly byte[] _demoTickInfo = new byte[DemoInfoSize];
	private readonly byte[][] _demoFrames = new byte[DemoFrameHistory][];
	private readonly byte[] _demoPacket = new byte[MaxDemoMessage];
	private readonly string[] _demos = new string[MaxDemos];
	private readonly int[] _gameGaugeSeconds = new int[MaxGameGaugeSeconds];
	private readonly byte[] _userCmdImage = new byte[UserCmd.Size];

	private bool _demoRecording;
	private bool _demoPlayback;
	private bool _timeDemo;
	private float _demoStartTime;
	private int _demoStartFrame;
	private int _forceTrack;
	private DemoFile _demoFile;
	private DemoFile _demoHeaderFile;
	private bool _demoWaiting;
	private bool _demoAppending;
	private string _demoFileName = "";
	private int _demoFrameCount;
	private int _timeDemoLastFrame;
	private int _timeDemoStartFrame;
	private float _timeDemoStartTime;
	private int _demoNum;
	private float _demoTimeScale = 1f;
	private DemoHeader _demoHeader = new();
	private List<DemoEntry> _demoDirectory;
	private DemoEntry _demoEntry;
	private int _demoEntryIndex;
	private bool _demoFadeOutStarted;
	private int _demoLastReadFrame;
	private int _gameGaugeCount;
	private float _gameGaugeTime;
	private int _gameGaugeFrame;
	private int _gameGaugeMin;
	private int _gameGaugeMax;
	private UserCmd _cmd;
	private bool _hostAbort;
	private int _demoServerCount;

	public bool DemoRecording => _demoRecording;

	public bool DemoPlayback => _demoPlayback;

	public bool TimeDemo => _timeDemo;

	private static string Fixed( double value, int decimals ) => value.ToString( $"F{decimals}", CultureInfo.InvariantCulture );

	private static string FileExtension( string name )
	{
		var dot = name.LastIndexOf( '.' );

		if ( dot < 0 )
			return "";

		var extension = name[(dot + 1)..];

		return extension.Length > 7 ? extension[..7] : extension;
	}

	private static bool HasExtension( string name, string extension ) => FileExtension( name ) == extension.TrimStart( '.' );

	private static string FixExtension( string name, string extension )
	{
		return HasExtension( name, extension ) ? name : $"{name}.{extension.TrimStart( '.' )}";
	}

	private static string StripExtension( string name )
	{
		for ( var i = name.Length - 1; i >= 0 && name[i] is not ('/' or '\\'); i-- )
		{
			if ( name[i] == '.' )
				return name[..i];
		}

		return name;
	}

	private static DemoFile OpenDemoForRead( string name )
	{
		return Server.ReadFile( name ) is { } data ? new DemoFile( name, data ) : null;
	}

	private static void CloseDemoForWrite( DemoFile file ) => Server.WriteFile( file.Name, file.ToArray() );

	private static void CopyFileData( DemoFile output, DemoFile input, int size )
	{
		var buffer = new byte[size];

		input.Read( buffer );
		output.Write( buffer );
	}

	private static List<DemoEntry> ReadDirectory( DemoFile file, int count )
	{
		var image = new byte[count * DemoEntry.Size];
		var entries = new List<DemoEntry>( count + 1 );

		file.Read( image );

		for ( var i = 0; i < count; i++ )
			entries.Add( DemoEntry.Read( image, i * DemoEntry.Size ) );

		return entries;
	}

	private static void WriteDirectory( DemoFile file, List<DemoEntry> entries )
	{
		var image = new byte[DemoEntry.Size];

		file.WriteInt( entries.Count );

		foreach ( var entry in entries )
		{
			Array.Clear( image );

			entry.Write( image, 0 );

			file.Write( image );
		}
	}

	private void WriteDemoCommandHeader( DemoFile file, int command )
	{
		file.WriteByte( command );
		file.WriteFloat( (float)Host.RealTime - _demoStartTime );
		file.WriteInt( Host.FrameCount - _demoStartFrame );
	}

	private void WriteMoveVars( byte[] image, int offset, MoveVars vars )
	{
		StructImage.Write( image, offset, vars.Gravity );
		StructImage.Write( image, offset + 4, vars.StopSpeed );
		StructImage.Write( image, offset + 8, vars.MaxSpeed );
		StructImage.Write( image, offset + 0xc, vars.SpectatorMaxSpeed );
		StructImage.Write( image, offset + 0x10, vars.Accelerate );
		StructImage.Write( image, offset + 0x14, vars.AirAccelerate );
		StructImage.Write( image, offset + 0x18, vars.WaterAccelerate );
		StructImage.Write( image, offset + 0x1c, vars.Friction );
		StructImage.Write( image, offset + 0x20, vars.EdgeFriction );
		StructImage.Write( image, offset + 0x24, vars.WaterFriction );
		StructImage.Write( image, offset + 0x28, vars.EntGravity );
		StructImage.Write( image, offset + 0x2c, vars.Bounce );
		StructImage.Write( image, offset + 0x30, vars.StepSize );
		StructImage.Write( image, offset + 0x34, vars.MaxVelocity );
		StructImage.Write( image, offset + 0x38, vars.ZMax );
		StructImage.Write( image, offset + 0x3c, vars.WaveHeight );
		StructImage.Write( image, offset + 0x40, vars.Footsteps );
		StructImage.Write( image, offset + 0x44, 32, vars.SkyName ?? "" );
		StructImage.Write( image, offset + 0x64, vars.RollAngle );
		StructImage.Write( image, offset + 0x68, vars.RollSpeed );
		StructImage.Write( image, offset + 0x6c, vars.SkyColorR );
		StructImage.Write( image, offset + 0x70, vars.SkyColorG );
		StructImage.Write( image, offset + 0x74, vars.SkyColorB );
		StructImage.Write( image, offset + 0x78, vars.SkyVecX );
		StructImage.Write( image, offset + 0x7c, vars.SkyVecY );
		StructImage.Write( image, offset + 0x80, vars.SkyVecZ );
	}

	private static void WriteRefParams( byte[] image, int offset, in RefParams p )
	{
		StructImage.Write( image, offset, p.ViewOrigin );
		StructImage.Write( image, offset + 0xc, p.ViewAngles );
		StructImage.Write( image, offset + 0x18, p.Forward );
		StructImage.Write( image, offset + 0x24, p.Right );
		StructImage.Write( image, offset + 0x30, p.Up );
		StructImage.Write( image, offset + 0x3c, p.FrameTime );
		StructImage.Write( image, offset + 0x40, p.Time );
		StructImage.Write( image, offset + 0x44, p.Intermission ? 1 : 0 );
		StructImage.Write( image, offset + 0x48, p.Paused ? 1 : 0 );
		StructImage.Write( image, offset + 0x4c, p.Spectator ? 1 : 0 );
		StructImage.Write( image, offset + 0x50, p.OnGround ? 1 : 0 );
		StructImage.Write( image, offset + 0x54, p.WaterLevel );
		StructImage.Write( image, offset + 0x58, p.SimVelocity );
		StructImage.Write( image, offset + 0x64, p.SimOrigin );
		StructImage.Write( image, offset + 0x70, p.ViewHeight );
		StructImage.Write( image, offset + 0x7c, p.IdealPitch );
		StructImage.Write( image, offset + 0x80, p.ClientViewAngles );
		StructImage.Write( image, offset + 0x8c, p.Health );
		StructImage.Write( image, offset + 0x90, p.CrosshairAngle );
		StructImage.Write( image, offset + 0x9c, p.ViewSize );
		StructImage.Write( image, offset + 0xa0, p.PunchAngle );
		StructImage.Write( image, offset + 0xac, p.MaxClients );
		StructImage.Write( image, offset + 0xb0, p.ViewEntity );
		StructImage.Write( image, offset + 0xb4, p.PlayerNum );
		StructImage.Write( image, offset + 0xb8, p.MaxEntities );
		StructImage.Write( image, offset + 0xbc, p.DemoPlayback ? 1 : 0 );
		StructImage.Write( image, offset + 0xc0, p.Hardware );
		StructImage.Write( image, offset + 0xc4, p.Smoothing ? 1 : 0 );
		StructImage.Write( image, offset + 0xc8, 0 );
		StructImage.Write( image, offset + 0xcc, 0 );
		StructImage.Write( image, offset + 0xd0, p.ViewportX );
		StructImage.Write( image, offset + 0xd4, p.ViewportY );
		StructImage.Write( image, offset + 0xd8, p.ViewportWidth );
		StructImage.Write( image, offset + 0xdc, p.ViewportHeight );
		StructImage.Write( image, offset + 0xe0, p.NextView );
		StructImage.Write( image, offset + 0xe4, p.OnlyClientDraw );
	}

	private static void ReadRefParams( byte[] image, int offset, ref RefParams p )
	{
		p.ViewOrigin = StructImage.ReadVector( image, offset );
		p.ViewAngles = StructImage.ReadVector( image, offset + 0xc );
		p.Forward = StructImage.ReadVector( image, offset + 0x18 );
		p.Right = StructImage.ReadVector( image, offset + 0x24 );
		p.Up = StructImage.ReadVector( image, offset + 0x30 );
		p.FrameTime = StructImage.ReadFloat( image, offset + 0x3c );
		p.Time = StructImage.ReadFloat( image, offset + 0x40 );
		p.Intermission = StructImage.ReadInt( image, offset + 0x44 ) != 0;
		p.Paused = StructImage.ReadInt( image, offset + 0x48 ) != 0;
		p.Spectator = StructImage.ReadInt( image, offset + 0x4c ) != 0;
		p.OnGround = StructImage.ReadInt( image, offset + 0x50 ) != 0;
		p.WaterLevel = StructImage.ReadInt( image, offset + 0x54 );
		p.SimVelocity = StructImage.ReadVector( image, offset + 0x58 );
		p.SimOrigin = StructImage.ReadVector( image, offset + 0x64 );
		p.ViewHeight = StructImage.ReadVector( image, offset + 0x70 );
		p.IdealPitch = StructImage.ReadFloat( image, offset + 0x7c );
		p.ClientViewAngles = StructImage.ReadVector( image, offset + 0x80 );
		p.Health = StructImage.ReadInt( image, offset + 0x8c );
		p.CrosshairAngle = StructImage.ReadVector( image, offset + 0x90 );
		p.ViewSize = StructImage.ReadFloat( image, offset + 0x9c );
		p.PunchAngle = StructImage.ReadVector( image, offset + 0xa0 );
		p.MaxClients = StructImage.ReadInt( image, offset + 0xac );
		p.ViewEntity = StructImage.ReadInt( image, offset + 0xb0 );
		p.PlayerNum = StructImage.ReadInt( image, offset + 0xb4 );
		p.MaxEntities = StructImage.ReadInt( image, offset + 0xb8 );
		p.DemoPlayback = StructImage.ReadInt( image, offset + 0xbc ) != 0;
		p.Hardware = StructImage.ReadInt( image, offset + 0xc0 );
		p.Smoothing = StructImage.ReadInt( image, offset + 0xc4 ) != 0;
		p.NextView = StructImage.ReadInt( image, offset + 0xe0 );
		p.OnlyClientDraw = StructImage.ReadInt( image, offset + 0xe4 );
	}

	private void SetDemoViewInfo( in RefParams refParams, Vector3 view, int viewModel )
	{
		WriteRefParams( _demoInfo, DemoRefParamsOffset, refParams );

		refParams.Cmd.Write( _userCmdImage );

		_userCmdImage.CopyTo( _demoInfo, DemoCmdOffset );

		WriteMoveVars( _demoInfo, DemoMoveVarsOffset, refParams.MoveVars );

		StructImage.Write( _demoInfo, DemoViewOffset, view );
		StructImage.Write( _demoInfo, DemoViewModelOffset, viewModel );
	}

	private void GetDemoViewInfo( ref RefParams refParams, ref Vector3 view, ref int viewModel )
	{
		var latest = _demoFrames[0];

		if ( latest is null )
			return;

		ReadRefParams( latest, DemoRefParamsOffset, ref refParams );

		if ( _timeDemo )
		{
			view = StructImage.ReadVector( latest, DemoViewOffset );
			viewModel = StructImage.ReadInt( latest, DemoViewModelOffset );

			return;
		}

		var time = (float)((double)((float)(_demoTimeScale * (Host.RealTime - CommandTime + _accumulator)) - _demoStartTime) - DemoInterpolationDelay);
		var to = latest;
		byte[] from = null;

		for ( var i = 1; i < DemoInterpolationFrames; i++ )
		{
			from = _demoFrames[i];

			if ( from is null )
				break;

			var timestamp = StructImage.ReadFloat( from, 0 );

			if ( timestamp == 0f || timestamp < time )
				break;

			to = from;
		}

		if ( from is null )
			return;

		var fromTime = StructImage.ReadFloat( from, 0 );
		var toTime = StructImage.ReadFloat( to, 0 );

		if ( fromTime == 0f || fromTime == toTime )
			return;

		var fraction = Math.Clamp( (time - fromTime) / (toTime - fromTime), 0f, 1f );

		refParams.ViewOrigin = DemoLerp( from, to, DemoRefParamsOffset, fraction );

		var fromAngles = StructImage.ReadVector( from, DemoRefParamsOffset + 0xc );
		var toAngles = StructImage.ReadVector( to, DemoRefParamsOffset + 0xc );
		var angles = default( Vector3 );

		for ( var i = 0; i < 3; i++ )
		{
			var delta = toAngles[i] - fromAngles[i];

			if ( delta > 180f )
				delta -= 360f;
			else if ( delta < -180f )
				delta += 360f;

			angles[i] = (delta * fraction) + fromAngles[i];
		}

		refParams.ViewAngles = MathLib.NormalizeAngles( angles );
		refParams.SimVelocity = DemoLerp( from, to, DemoRefParamsOffset + 0x58, fraction );
		refParams.SimOrigin = DemoLerp( from, to, DemoRefParamsOffset + 0x64, fraction );
		refParams.ViewHeight = DemoLerp( from, to, DemoRefParamsOffset + 0x70, fraction );

		view = DemoLerp( from, to, DemoViewOffset, fraction );
		viewModel = StructImage.ReadInt( to, DemoViewModelOffset );
	}

	private static Vector3 DemoLerp( byte[] from, byte[] to, int offset, float fraction )
	{
		var start = StructImage.ReadVector( from, offset );
		var delta = StructImage.ReadVector( to, offset ) - start;

		return start + (delta * fraction);
	}

	private void BeginDemoStartup()
	{
		_demoHeaderFile = new DemoFile( "demoheader.dmf" );

		Log.Trace( "Spooling demo header.\n" );
	}

	private void WriteDemoInfo( DemoFile file )
	{
		var info = _demoTickInfo;

		_demoInfo.CopyTo( info, 0 );

		var simOrigin = StructImage.ReadVector( info, DemoRefParamsOffset + 0x64 );
		var viewHeight = StructImage.ReadVector( info, DemoRefParamsOffset + 0x70 );
		var offset = _simOrg - simOrigin + _viewHeight - viewHeight;

		StructImage.Write( info, DemoRefParamsOffset, StructImage.ReadVector( info, DemoRefParamsOffset ) + offset );
		StructImage.Write( info, DemoRefParamsOffset + 0x64, _simOrg );
		StructImage.Write( info, DemoRefParamsOffset + 0x70, _viewHeight );
		StructImage.Write( info, DemoViewOffset, StructImage.ReadVector( info, DemoViewOffset ) + offset );

		file.Write( info );
	}

	private void WriteDemoNetchan( DemoFile file, int length )
	{
		WriteDemoInfo( file );
		file.WriteInt( _netchan.IncomingSequence );
		file.WriteInt( _netchan.IncomingAcknowledged );
		file.WriteInt( _netchan.IncomingReliableAcknowledged );
		file.WriteInt( _netchan.IncomingReliableSequence );
		file.WriteInt( _netchan.OutgoingSequence );
		file.WriteInt( _netchan.ReliableSequence );
		file.WriteInt( _netchan.LastReliableSequence );
		file.WriteInt( length );
	}

	private void WriteDemoStartup( int start, MessageReader message )
	{
		var length = message.Size - start;

		if ( _demoHeaderFile is null || length <= 0 )
			return;

		WriteDemoCommandHeader( _demoHeaderFile, DemoNetworkStartup );
		WriteDemoNetchan( _demoHeaderFile, length );

		_demoHeaderFile.Write( message.Data.AsSpan( start, length ) );
	}

	private void WriteDemoMessage( int start, MessageReader message )
	{
		var length = message.Size - start;

		if ( !_demoRecording || _demoFile is null || length < 0 )
			return;

		_demoFrameCount++;

		WriteDemoCommandHeader( _demoFile, DemoNetwork );
		WriteDemoNetchan( _demoFile, length );

		_demoFile.Write( message.Data.AsSpan( start, length ) );
	}

	private void WriteDemoMessages( int start, MessageReader message, ClientState state )
	{
		if ( _demoPlayback )
			return;

		if ( state != ClientState.Active )
			WriteDemoStartup( start, message );

		if ( _demoRecording && !_demoWaiting )
			WriteDemoMessage( start, message );
	}

	private void WriteDemoCommand( string command )
	{
		var image = new byte[DemoCommandSize];

		StructImage.Write( image, 0, DemoCommandSize, command.Length >= DemoCommandSize ? command[..(DemoCommandSize - 1)] : command );

		WriteDemoCommandHeader( _demoFile, DemoConsoleCommand );

		_demoFile.Write( image );
	}

	public void RecordHudCommand( string command )
	{
		if ( _demoRecording && !_spectator )
			WriteDemoCommand( command );
	}

	private void WriteDemoClientData( in ClientData data )
	{
		if ( _demoFile is null )
			return;

		var image = new byte[DemoClientDataSize];

		StructImage.Write( image, 0, data.Origin );
		StructImage.Write( image, 0xc, data.ViewAngles );
		StructImage.Write( image, 0x18, data.WeaponBits );
		StructImage.Write( image, 0x1c, data.Fov );

		WriteDemoCommandHeader( _demoFile, DemoClientData );

		_demoFile.Write( image );
	}

	private void WriteDemoEvent( int flags, int index, float delay, in EventArgs args )
	{
		if ( _demoFile is null )
			return;

		WriteDemoCommandHeader( _demoFile, DemoEvent );

		_demoFile.WriteInt( flags );
		_demoFile.WriteInt( index );
		_demoFile.WriteFloat( delay );

		args.Write( _eventImage );

		_demoFile.Write( _eventImage );
	}

	private void WriteDemoWeaponAnim( int sequence, int body )
	{
		if ( _demoFile is null )
			return;

		WriteDemoCommandHeader( _demoFile, DemoWeaponAnim );

		_demoFile.WriteInt( sequence );
		_demoFile.WriteInt( body );
	}

	private void WriteDemoSound( int channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		if ( _demoFile is null )
			return;

		var name = Encoding.Latin1.GetBytes( sample );

		WriteDemoCommandHeader( _demoFile, DemoSound );

		_demoFile.WriteInt( channel );
		_demoFile.WriteInt( name.Length );
		_demoFile.Write( name );
		_demoFile.WriteFloat( volume );
		_demoFile.WriteFloat( attenuation );
		_demoFile.WriteInt( flags );
		_demoFile.WriteInt( pitch );
	}

	public void WriteDemoUserBuffer( int size, byte[] buffer )
	{
		if ( !_demoRecording || _demoFile is null || size < 0 )
			return;

		WriteDemoCommandHeader( _demoFile, DemoBuffer );

		_demoFile.WriteInt( size );
		_demoFile.Write( buffer.AsSpan( 0, size ) );
	}

	private void ReadDemoSound()
	{
		var channel = _demoFile.ReadInt();
		var length = Math.Min( (uint)_demoFile.ReadInt(), 0xffu );
		var name = new byte[length];

		_demoFile.Read( name );

		var first = _demoFile.ReadFloat();
		var second = _demoFile.ReadFloat();
		var flags = _demoFile.ReadInt();
		var pitch = _demoFile.ReadInt();
		var sample = StructImage.ReadString( name, 0, name.Length );

		Sfx sfx;

		if ( (flags & SoundSentenceFlag) != 0 )
		{
			sfx = new Sfx { Name = $"!{Sound.SentenceName( Common.Atoi( sample.Length > 1 ? sample[1..] : "" ) )}" };
		}
		else
		{
			sfx = Sound.PrecacheSound( sample );

			if ( sfx is null )
				return;
		}

		if ( channel == (int)SoundChannel.Static )
			Sound.StartStaticSound( _playerNum + 1, (int)SoundChannel.Static, sfx, _simOrg, second, first, flags, pitch );
		else
			Sound.StartDynamicSound( _playerNum + 1, channel, sfx, _simOrg, second, first, flags, pitch );
	}

	private void ReadDemoUserBuffer()
	{
		var buffer = new byte[MaxDemoBuffer];
		var size = (int)Math.Min( (uint)_demoFile.ReadInt(), (uint)MaxDemoBuffer );

		_demoFile.Read( buffer.AsSpan( 0, size ) );

		_clientDll.DemoReadBuffer( size, buffer );
	}

	private void DemoScreenFade( int duration, int holdTime, int flags )
	{
		var image = new byte[10];

		BitConverter.TryWriteBytes( image.AsSpan( 0 ), (ushort)duration );
		BitConverter.TryWriteBytes( image.AsSpan( 2 ), (ushort)holdTime );
		BitConverter.TryWriteBytes( image.AsSpan( 4 ), (short)flags );

		image[9] = 0xff;

		ScreenFadeMessage( "ScreenFade", image );
	}

	private void EndGame( string message )
	{
		Log.Trace( $"Host_EndGame: {message}\n" );

		var demoNum = _demoNum;

		if ( Server.Get( Scene ) is { Active: true } server )
			server.ShutdownServer();

		_demoNum = demoNum;

		if ( demoNum != -1 )
		{
			DisconnectCommand();

			_demoNum = demoNum;

			NextDemo();

			_hostAbort = true;

			return;
		}

		Disconnect();

		Cmd.AddText( "cd stop\n" );
		Cmd.Execute();

		_hostAbort = true;
	}

	private void DisconnectCommand()
	{
		Disconnect();

		if ( Server.Get( Scene ) is { Active: true } server )
			server.ShutdownServer();
	}

	private bool ReadDemoMessage()
	{
		if ( _demoFile is null )
		{
			_demoPlayback = false;

			DisconnectCommand();

			Log.Info( "Tried to read a demo message with no demo file\n" );

			return false;
		}

		float elapsed;

		while ( true )
		{
			var position = _demoFile.Position;
			var command = _demoFile.ReadByte();
			var frameTime = _demoFile.ReadFloat();

			_demoFile.ReadInt();

			elapsed = (float)(_demoTimeScale * Host.RealTime) - _demoStartTime;

			if ( command != DemoNetworkStartup && command != DemoNextSection && !_timeDemo && elapsed + (CommandTime * 0.5) <= frameTime )
			{
				_demoFile.Position = position;

				return false;
			}

			if ( command == DemoStart )
			{
				_demoStartFrame = Host.FrameCount;
				_demoStartTime = (float)(_demoTimeScale * Host.RealTime);

				continue;
			}

			if ( command == DemoNextSection )
			{
				if ( _demoEntryIndex > 0 && _demoEntry is not null && (_demoEntry.Flags & DemoEntry.FlagPlay) != 0 )
					Cmd.AddText( "cd stop\n" );

				_demoEntryIndex++;

				if ( _demoDirectory.Count <= _demoEntryIndex )
					{
						EndGame( "End of demo.\n" );

						return false;
					}

				_demoEntry = _demoDirectory[_demoEntryIndex];
				_forceTrack = _demoEntry.CdTrack;
				_demoFile.Position = _demoEntry.Offset;
				_demoStartFrame = Host.FrameCount;
				_demoFrameCount = 0;
				_demoStartTime = (float)(_demoTimeScale * Host.RealTime);

				continue;
			}

			if ( command == DemoConsoleCommand )
			{
				var image = new byte[DemoCommandSize];

				_demoFile.Read( image );

				var text = StructImage.ReadString( image, 0, DemoCommandSize );

				if ( ValidStuffText( text ) )
				{
					Cmd.AddText( text );
					Cmd.AddText( "\n" );
				}

				continue;
			}

			if ( command == DemoClientData )
			{
				var image = new byte[DemoClientDataSize];

				_demoFile.Read( image );

				var data = new ClientData
				{
					Origin = StructImage.ReadVector( image, 0 ),
					ViewAngles = StructImage.ReadVector( image, 0xc ),
					WeaponBits = StructImage.ReadInt( image, 0x18 ),
					Fov = StructImage.ReadFloat( image, 0x1c )
				};

				if ( _clientDll.UpdateClientData( ref data, (float)ClientTime ) && !_spectator )
				{
					ViewAngles = data.ViewAngles;
					FieldOfView = data.Fov;
				}

				FieldOfView = StructImage.ReadFloat( image, 0x1c );

				continue;
			}

			if ( command == DemoEvent )
			{
				var flags = _demoFile.ReadInt();
				var index = _demoFile.ReadInt();
				var delay = _demoFile.ReadFloat();

				_demoFile.Read( _eventImage );

				QueueEvent( flags, index, delay, EventArgs.Read( _eventImage ) );

				continue;
			}

			if ( command == DemoWeaponAnim )
			{
				var sequence = _demoFile.ReadInt();
				var body = _demoFile.ReadInt();

				WeaponAnimation( sequence, body );

				continue;
			}

			if ( command == DemoSound )
			{
				ReadDemoSound();

				continue;
			}

			if ( command == DemoBuffer )
			{
				ReadDemoUserBuffer();

				continue;
			}

			break;
		}

		if ( _timeDemo && _demoLastReadFrame == Host.FrameCount )
		{
			_demoFile.Position -= 9;

			return false;
		}

		_demoLastReadFrame = Host.FrameCount;

		if ( Host.FrameCount == _timeDemoStartFrame + 1 )
			_timeDemoStartTime = (float)Host.RealTime;

		if ( _demoEntryIndex != 0 )
		{
			if ( _demoFrameCount == 0 && _demoEntry is not null )
			{
				if ( (_demoEntry.Flags & DemoEntry.FlagPlay) != 0 )
					Cmd.AddText( $"cd stop\ncd play {_demoEntry.CdTrack}\n" );

				if ( (_demoEntry.Flags & DemoEntry.FlagFadeInFast) != 0 )
					DemoScreenFade( 0x800, 0, 0 );

				if ( (_demoEntry.Flags & DemoEntry.FlagFadeInSlow) != 0 )
					DemoScreenFade( 0x2000, 0, 0 );
			}

			if ( _demoFrameCount == 1 )
			{
				_demoFadeOutStarted = false;

				if ( (_demoEntry.Flags & DemoEntry.FlagTitle) != 0 )
				{
					SetDemoMessage( _demoEntry.Description, 0.01f, 1f, 2f );
					HudMessage( DemoMessageName );
				}

				if ( !_timeDemo )
					_demoStartTime = (float)(_demoTimeScale * Host.RealTime);
			}
		}

		_demoFrameCount++;

		if ( _demoEntryIndex != 0 && !_demoFadeOutStarted )
		{
			if ( (_demoEntry.Flags & DemoEntry.FlagFadeOutFast) != 0 && _demoEntry.TrackTime + DemoInterpolationDelay <= elapsed + 0.5 )
			{
				_demoFadeOutStarted = true;

				DemoScreenFade( 0x800, 0x199, 1 );
			}

			if ( (_demoEntry.Flags & DemoEntry.FlagFadeOutSlow) != 0 && !_demoFadeOutStarted && _demoEntry.TrackTime + DemoInterpolationDelay <= elapsed + 2.0 )
			{
				_demoFadeOutStarted = true;

				DemoScreenFade( 0x2000, 0x199, 1 );
			}
		}

		var info = _demoFrames[DemoFrameHistory - 1] ?? new byte[DemoInfoSize];

		for ( var i = DemoFrameHistory - 1; i > 0; i-- )
			_demoFrames[i] = _demoFrames[i - 1];

		_demoFrames[0] = info;

		Array.Clear( info );

		_demoFile.Read( info );

		Array.Copy( info, DemoCmdOffset, _demoInfo, DemoCmdOffset, DemoInfoSize - DemoCmdOffset );

		StructImage.Write( info, 0, elapsed );

		_netchan.IncomingSequence = _demoFile.ReadInt();
		_netchan.IncomingAcknowledged = _demoFile.ReadInt();
		_netchan.IncomingReliableAcknowledged = _demoFile.ReadInt();
		_netchan.IncomingReliableSequence = _demoFile.ReadInt();
		_netchan.OutgoingSequence = _demoFile.ReadInt();
		_netchan.ReliableSequence = _demoFile.ReadInt();
		_netchan.LastReliableSequence = _demoFile.ReadInt();

		Span<byte> lengthImage = stackalloc byte[4];

		if ( _demoFile.Read( lengthImage ) != 4 )
			{
				EndGame( "Bad demo length.\n" );

				return false;
			}

		var length = BitConverter.ToInt32( lengthImage );

		if ( length < 0 )
			{
				EndGame( "Demo message length < 0.\n" );

				return false;
			}

		if ( length > MaxDemoMessage )
			{
				EndGame( "Demo message > MAX_POSSIBLE_MSG" );

				return false;
			}

		if ( length > 0 && _demoFile.Read( _demoPacket.AsSpan( 0, length ) ) != length )
			{
				EndGame( "Error reading demo message data.\n" );

				return false;
			}

		_netMessage.BeginReading( _demoPacket, length );

		return true;
	}

	private void ReadDemoPackets()
	{
		while ( ReadDemoMessage() )
		{
			if ( _netMessage.Size >= 4 && BitConverter.ToInt32( _netMessage.Data, 0 ) == -1 )
			{
				ConnectionlessPacket( StructImage.ReadString( _netMessage.Data, 4, _netMessage.Size - 4 ) );
				continue;
			}

			if ( _state is ClientState.Disconnected or ClientState.Connecting )
				continue;

			ParseServerMessage( _netMessage );
		}
	}

	private void StopPlayback()
	{
		if ( !_demoPlayback )
			return;

		_demoPlayback = false;
		_demoFile = null;
		_state = ClientState.Disconnected;
		_demoFrameCount = 0;
		_demoDirectory = null;

		if ( GameGauge != 0f )
		{
			Cvar.Set( "cl_gg", "0" );

			var time = (float)(Host.RealTime - _timeDemoStartTime);

			if ( time == 0f )
				time = 1f;

			var fps = (double)((float)(Host.FrameCount - _timeDemoStartFrame - 1) / time);
			var report = new StringBuilder();

			report.Append( $"{Fixed( fps, 2 ),5} Half-Life v{Host.VersionString()}\n" );
			report.Append( $"{_gameGaugeMin} Min\n" );
			report.Append( $"{_gameGaugeMax} Max\n" );

			for ( var i = 0; i < _gameGaugeCount; i++ )
				report.Append( $"{_gameGaugeSeconds[i]} Second {i + 1}\n" );

			Log.Info( report.ToString() );

			Server.WriteFile( "fps.txt", Encoding.Latin1.GetBytes( report.ToString() ) );

			Cmd.AddText( "quit\n" );
		}

		if ( _timeDemo )
		{
			var frames = Host.FrameCount - _timeDemoStartFrame - 1;

			_timeDemo = false;

			var time = (float)(Host.RealTime - _timeDemoStartTime);

			if ( time == 0f )
				time = 1f;

			Log.Info( $"{frames} frames {Fixed( time, 3 ),5} seconds {Fixed( frames / time, 3 ),5} fps\n" );
		}

		Cmd.AddText( "cd stop\n" );
	}

	private void GameGaugeFrame( float time )
	{
		var frame = Host.FrameCount;

		if ( _gameGaugeTime == 0f )
		{
			_gameGaugeTime = time;
			return;
		}

		if ( time - _gameGaugeTime < 1f )
			return;

		_gameGaugeFrame = frame - _gameGaugeFrame;

		if ( _gameGaugeMax < _gameGaugeFrame )
			_gameGaugeMax = _gameGaugeFrame;

		if ( _gameGaugeFrame < _gameGaugeMin )
			_gameGaugeMin = _gameGaugeFrame;

		if ( _gameGaugeCount < MaxGameGaugeSeconds )
			_gameGaugeSeconds[_gameGaugeCount++] = _gameGaugeFrame;

		_gameGaugeFrame = frame;
		_gameGaugeTime = time;
	}

	private void NextDemo()
	{
		if ( _demoNum == -1 )
			return;

		BeginLoadingPlaque();

		if ( _demoNum == MaxDemos || string.IsNullOrEmpty( _demos[_demoNum] ) )
		{
			_demoNum = 0;

			if ( string.IsNullOrEmpty( _demos[0] ) )
			{
				Log.Info( "No demos listed with startdemos\n" );

				_demoNum = -1;

				return;
			}
		}

		Cmd.InsertText( $"playdemo {_demos[_demoNum]}\n" );

		_demoNum++;
	}

	private void Record( string[] arguments )
	{
		var count = arguments.Length + 1;

		if ( count < 2 )
		{
			Log.Info( "record <demoname> <cd>\n" );
			return;
		}

		if ( _demoRecording )
		{
			Log.Info( "Already recording.\n" );
			return;
		}

		if ( _demoPlayback )
		{
			Log.Info( "Can't record during demo playback.\n" );
			return;
		}

		if ( _demoHeaderFile is null || _state != ClientState.Active )
		{
			Log.Info( $"You must be in an active map spawned on a machine that allows creation of files in {Server.GameDirectory}\n" );
			return;
		}

		if ( count != 2 && count != 3 )
		{
			Log.Info( "record <demoname> <cd track>\n" );
			return;
		}

		if ( arguments[0].Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed.\n" );
			return;
		}

		var track = -1;

		if ( count == 3 )
		{
			track = Common.Atoi( arguments[1] );

			Log.Info( $"Forcing CD track to {track}\n" );
		}

		var name = FixExtension( arguments[0], ".dem" );

		if ( !HasExtension( name, "dem" ) )
		{
			Log.Info( $"Error: couldn't open demo file {name}\n" );
			return;
		}

		Log.Info( $"recording to {name}.\n" );

		_demoFile = new DemoFile( name );

		_demoHeader = new DemoHeader
		{
			Stamp = DemoHeader.FileStamp,
			MapName = Common.FileBase( _levelName ),
			GameDirectory = Common.FileBase( Server.GameDirectory ),
			MapCrc = _serverCrc,
			DemoProtocol = DemoProtocol,
			NetProtocol = Host.ProtocolVersion,
			DirectoryOffset = 0
		};

		_demoHeader.Write( _demoFile );

		_demoDirectory = [new DemoEntry(), new DemoEntry()];
		_demoEntry = _demoDirectory[0];
		_demoEntry.Description = "LOADING";
		_demoEntry.Type = 0;
		_demoEntry.Flags = 0;
		_demoEntry.CdTrack = track;
		_demoEntry.TrackTime = 0f;
		_demoEntry.Offset = _demoFile.Position;

		_demoWaiting = true;
		_demoRecording = true;

		WriteDemoCommandHeader( _demoHeaderFile, DemoNextSection );

		var headerSize = _demoHeaderFile.Position;

		_demoHeaderFile.Position = 0;

		CopyFileData( _demoFile, _demoHeaderFile, headerSize );

		_demoHeaderFile.Position = headerSize;

		_demoStartTime = (float)Host.RealTime;
		_demoFrameCount = 0;
		_demoStartFrame = Host.FrameCount;

		_demoEntry.Length = _demoFile.Position - _demoEntry.Offset;

		_demoEntry = _demoDirectory[1];
		_demoEntry.Type = 1;
		_demoEntry.Description = "Playback";
		_demoEntry.Flags = 0;
		_demoEntry.CdTrack = track;
		_demoEntry.TrackTime = 0f;
		_demoEntry.Offset = _demoFile.Position;

		WriteDemoCommandHeader( _demoFile, DemoStart );

		_timeDemoStartFrame = Host.FrameCount;
		_timeDemoLastFrame = -1;
		_demoAppending = false;
	}

	private void StopRecording()
	{
		if ( Cmd.FromClient )
			return;

		if ( !_demoRecording )
		{
			Log.Info( "Not recording a demo.\n" );
			return;
		}

		_demoFile.WriteByte( DemoNextSection );

		var time = (float)Host.RealTime;

		_demoFile.WriteFloat( time - _demoStartTime );
		_demoFile.WriteInt( Host.FrameCount - _demoStartFrame );

		var position = _demoFile.Position;

		_demoEntry.Length = position - _demoEntry.Offset;
		_demoEntry.TrackTime = time - _demoStartTime;
		_demoEntry.Frames = _demoFrameCount;

		WriteDirectory( _demoFile, _demoDirectory );

		_demoDirectory = null;
		_demoHeader.DirectoryOffset = position;

		_demoFile.Position = 0;

		_demoHeader.Write( _demoFile );

		var file = _demoFile;

		_demoFile = null;
		_demoRecording = false;
		_timeDemoLastFrame = Host.FrameCount;

		if ( _demoAppending )
		{
			Log.Info( "Replacing old demo with appended version.\n" );

			Server.WriteFile( _demoFileName, file.ToArray() );
		}
		else
		{
			CloseDemoForWrite( file );
		}

		Log.Info( "Completed demo\n" );
		Log.Trace( $"Recording time {Fixed( time - _demoStartTime, 2 )}\nHost frames {_timeDemoLastFrame - _timeDemoStartFrame}\n" );
	}

	private void PlayDemo( string[] arguments )
	{
		var count = arguments.Length + 1;

		if ( count != 2 && count != 3 )
		{
			Log.Info( "playdemo <demoname> <replayspeed>: plays a demo\n" );
			Log.Info( "replayspeed == 1.0 default\n" );
			return;
		}

		_demoTimeScale = 1f;

		if ( count == 3 )
		{
			_demoTimeScale = Common.Atof( arguments[1] );

			if ( _demoTimeScale <= MinDemoTimeScale )
				_demoTimeScale = 1f;

			Log.Info( $"Playdemo:  Time scale {Fixed( _demoTimeScale, 6 )}\n" );
		}

		Disconnect();

		var name = Common.DefaultExtension( arguments[0].Length > 0xfb ? arguments[0][..0xfb] : arguments[0], ".dem" );

		Log.Info( $"Playing demo from {name}.\n" );

		_demoFile = OpenDemoForRead( name );

		if ( _demoFile is null )
		{
			Log.Info( "ERROR: couldn't open.\n" );

			_demoNum = -1;

			return;
		}

		_demoHeader = DemoHeader.Read( _demoFile );

		if ( !_demoHeader.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );

			_demoFile = null;
			_demoNum = -1;

			return;
		}

		if ( _demoHeader.NetProtocol != Host.ProtocolVersion || _demoHeader.DemoProtocol != DemoProtocol )
			Log.Info( $"WARNING! demo protocol outdated.\nDemo file protocols {_demoHeader.NetProtocol}:{_demoHeader.DemoProtocol}, Engine protocol is at {Host.ProtocolVersion}:{DemoProtocol}\n" );

		_demoFile.Position = _demoHeader.DirectoryOffset;

		var entries = _demoFile.ReadInt();

		if ( (uint)(entries - 1) >= MaxDemoEntries )
		{
			ExplainDisconnection( true, "Error: Corrupt demo file." );

			_demoFile = null;
			_demoNum = -1;

			Disconnect();

			return;
		}

		_demoDirectory = ReadDirectory( _demoFile, entries );
		_demoEntryIndex = 0;
		_demoEntry = _demoDirectory[0];
		_demoFile.Position = _demoEntry.Offset;
		_forceTrack = _demoEntry.CdTrack;
		_demoStartFrame = Host.FrameCount;
		_demoStartTime = (float)(_demoTimeScale * Host.RealTime);
		_demoPlayback = true;
		_state = ClientState.Connected;
		_spectator = false;

		_netchan.Setup( true, false, "", DiscardPacket, BlockSize );

		_nextCmdTime = (float)Host.RealTime;
		_demoFrameCount = 0;
		_lastOutgoingCommand = -1;

		Array.Clear( _demoFrames );

		_clientDll.DispatchUserMsg( "InitHUD", [] );
	}

	private static void DiscardPacket( byte[] packet )
	{
	}

	private void TimeDemoCommand( string[] arguments )
	{
		if ( arguments.Length + 1 != 2 )
		{
			Log.Info( "timedemo <demoname>: gets demo speeds\n" );
			return;
		}

		PlayDemo( arguments );

		_timeDemoStartTime = (float)Host.RealTime;
		_timeDemo = true;
		_timeDemoStartFrame = Host.FrameCount;
		_timeDemoLastFrame = -1;
	}

	private void GameGaugeCommand( string[] arguments )
	{
		if ( arguments.Length + 1 != 2 )
		{
			Log.Info( "gg <demoname> : Game Gauge 99\n" );
			return;
		}

		PlayDemo( arguments );

		Cvar.Set( "cl_gg", "1" );

		_timeDemoStartTime = (float)Host.RealTime;
		_gameGaugeMax = 0;
		_gameGaugeMin = int.MaxValue;
		_gameGaugeFrame = Host.FrameCount;
		_gameGaugeCount = 0;
		_gameGaugeTime = 0f;
		_timeDemo = true;
		_timeDemoStartFrame = Host.FrameCount;
		_timeDemoLastFrame = -1;
	}

	private void ListDemo( string[] arguments )
	{
		if ( _demoRecording || _demoPlayback )
		{
			Log.Info( "Listdemo only available when not running or recording.\n" );
			return;
		}

		var name = Common.DefaultExtension( arguments.Length > 0 ? arguments[0] : "", ".dem" );

		Log.Info( $"Demo contents for {name}.\n" );

		var file = OpenDemoForRead( name );

		if ( file is null )
		{
			Log.Info( "ERROR: couldn't open.\n" );
			return;
		}

		var header = DemoHeader.Read( file );

		if ( !header.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );
			return;
		}

		Log.Info( $"Demo file protocols {header.NetProtocol}N:{header.DemoProtocol}D\nServer protocols at {Host.ProtocolVersion}N:{DemoProtocol}D\n" );
		Log.Info( $"Map name :  {header.MapName}\n" );
		Log.Info( $"Game .dll:  {header.GameDirectory}\n" );

		file.Position = header.DirectoryOffset;

		var count = file.ReadInt();

		if ( (uint)(count - 1) >= MaxDemoEntries )
		{
			Log.Info( $"ERROR: demo had bogus # of directory entries:  {count}\n" );
			return;
		}

		var entries = ReadDirectory( file, count );

		Log.Info( "\n" );

		for ( var i = 0; i < entries.Count; i++ )
		{
			var entry = entries[i];
			var type = entry.Type == 0 ? "Start segment" : "Normal segment";

			Log.Info( $"{i + 1}:  {type}\n  \"{entry.Description,20}\"\nTime:  {Fixed( entry.TrackTime, 2 )} s.\nFrames:  {entry.Frames}\nSize:  {Fixed( entry.Length * 0.0009765625, 2 )}K\n" );

			if ( entry.Flags != 0 )
			{
				Log.Info( "  Flags:\n" );

				if ( (entry.Flags & DemoEntry.FlagTitle) != 0 )
					Log.Info( $"     Title \"{entry.Description}\"\n" );

				if ( (entry.Flags & DemoEntry.FlagPlay) != 0 )
					Log.Info( $"     Play {entry.CdTrack}\n" );

				if ( (entry.Flags & DemoEntry.FlagFadeInSlow) != 0 )
					Log.Info( "     Fade in slow\n" );

				if ( (entry.Flags & DemoEntry.FlagFadeInFast) != 0 )
					Log.Info( "     Fade in fast\n" );

				if ( (entry.Flags & DemoEntry.FlagFadeOutSlow) != 0 )
					Log.Info( "     Fade out slow\n" );

				if ( (entry.Flags & DemoEntry.FlagFadeOutFast) != 0 )
					Log.Info( "     Fade out fast\n" );
			}

			Log.Info( "\n" );
		}
	}

	private DemoFile OpenDemoForEdit( string name, out DemoFile output, out DemoHeader header )
	{
		output = null;
		header = null;

		var input = OpenDemoForRead( name );

		if ( input is null )
			return null;

		output = new DemoFile( name );

		CopyFileData( output, input, input.Length );

		input.Position = 0;
		output.Position = 0;

		header = DemoHeader.Read( input );

		return input;
	}

	private static bool CheckDemoProtocol( DemoHeader header )
	{
		if ( header.NetProtocol == Host.ProtocolVersion && header.DemoProtocol == DemoProtocol )
			return true;

		Log.Info( $"ERROR: demo protocol outdated\nDemo file protocols {header.NetProtocol}N:{header.DemoProtocol}D\nServer protocol is at {Host.ProtocolVersion}N:{DemoProtocol}D\n" );

		return false;
	}

	private void AppendDemo( string[] arguments )
	{
		if ( _demoRecording || _demoPlayback )
		{
			Log.Info( "Appenddemo only available when not running or recording a demo.\n" );
			return;
		}

		if ( _state != ClientState.Active )
		{
			Log.Info( $"You must be in an active map spawned on a machine that allows creation of files in {Server.GameDirectory}\n" );
			return;
		}

		var count = arguments.Length + 1;

		if ( count != 2 && count != 3 )
		{
			Log.Info( "appenddemo <demoname> <cd track>\n" );
			return;
		}

		if ( arguments[0].Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed.\n" );
			return;
		}

		var track = -1;

		if ( count == 3 )
		{
			track = Common.Atoi( arguments[1] );

			Log.Info( $"Forcing CD track to {track}\n" );
		}

		var name = Common.DefaultExtension( arguments[0], ".dem" );

		if ( !HasExtension( name, "dem" ) )
		{
			Log.Info( $"Error: couldn't open {name}, demo files must have .dem extension.\n" );
			return;
		}

		Log.Info( $"Appending to demo {name}.\n" );

		_demoFileName = name;

		var input = OpenDemoForRead( name );

		if ( input is null )
		{
			Log.Info( $"Error:  couldn't open demo file {name} for appending.\n" );
			return;
		}

		var temporary = FixExtension( StripExtension( name ), ".dm2" );

		_demoFile = new DemoFile( temporary );

		CopyFileData( _demoFile, input, input.Length );

		_demoFile.Position = 0;
		_demoHeader = DemoHeader.Read( _demoFile );

		if ( !_demoHeader.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );

			_demoFile = null;

			return;
		}

		if ( !_demoHeader.MapName.Equals( Common.FileBase( _levelName ), StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Info( $"{name} was recorded on a different map, cannot append.\n" );

			_demoFile = null;

			return;
		}

		if ( !CheckDemoProtocol( _demoHeader ) )
		{
			_demoFile = null;
			return;
		}

		_demoFile.Position = _demoHeader.DirectoryOffset;

		var entries = _demoFile.ReadInt();

		if ( (uint)(entries - 1) >= MaxDemoEntries )
		{
			Log.Info( $"ERROR: demo had bogus # of directory entries:  {entries}\n" );

			_demoFile = null;

			return;
		}

		_demoDirectory = ReadDirectory( _demoFile, entries );
		_demoEntryIndex = entries;
		_demoEntry = new DemoEntry();
		_demoDirectory.Add( _demoEntry );

		_demoFile.Position = _demoHeader.DirectoryOffset;

		_demoEntry.Type = 1;
		_demoEntry.Description = $"Playback '{_demoDirectory.Count - 1}'";
		_demoEntry.Flags = 0;
		_demoEntry.CdTrack = track;
		_demoEntry.TrackTime = 0f;
		_demoEntry.Offset = _demoFile.Position;

		_demoStartTime = (float)Host.RealTime;
		_demoStartFrame = Host.FrameCount;
		_demoFrameCount = 0;

		WriteDemoCommandHeader( _demoFile, DemoStart );

		_demoWaiting = true;
		_demoRecording = true;
		_timeDemoStartFrame = Host.FrameCount;
		_timeDemoLastFrame = -1;
		_demoAppending = true;
	}

	private void RemoveDemo( string[] arguments )
	{
		if ( _demoRecording || _demoPlayback )
		{
			Log.Info( "Removedemo only available when not running or recording a demo.\n" );
			return;
		}

		if ( arguments.Length + 1 != 3 )
		{
			Log.Info( "removedemo <demoname> <segment to remove>\nSegment 1 cannot be removed\n" );
			return;
		}

		if ( arguments[0].Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed.\n" );
			return;
		}

		var segment = Common.Atoi( arguments[1] );

		if ( segment < 2 )
		{
			Log.Info( "Cannot remove the STARTUP segment.\n" );
			return;
		}

		var name = Common.DefaultExtension( arguments[0], ".dem" );

		if ( !HasExtension( name, "dem" ) )
		{
			Log.Info( $"Error: couldn't open demo file {name}, demo files must have .dem extension.\n" );
			return;
		}

		Log.Info( $"Removing segment {segment} from demo {name}.\n" );

		var input = OpenDemoForRead( name );

		if ( input is null )
		{
			Log.Info( $"Error: couldn't open demo file {name} for segment removal.\n" );
			return;
		}

		var output = new DemoFile( name );
		var header = DemoHeader.Read( input );

		header.Write( output );

		if ( !header.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );
			return;
		}

		if ( !CheckDemoProtocol( header ) )
			return;

		input.Position = header.DirectoryOffset;

		var count = input.ReadInt();

		if ( (uint)(count - 1) >= MaxDemoEntries )
		{
			Log.Info( $"ERROR: demo had bogus # of directory entries:  {count}\n" );
			return;
		}

		if ( count == 1 )
		{
			Log.Info( "ERROR: Can't remove last directory entry.\n" );
			return;
		}

		var entries = ReadDirectory( input, count );
		var kept = new List<DemoEntry>( count - 1 );

		for ( var i = 0; i < entries.Count; i++ )
		{
			if ( i == segment - 1 )
				continue;

			var entry = entries[i].Clone();

			entry.Offset = output.Position;

			input.Position = entries[i].Offset;

			CopyFileData( output, input, entry.Length );

			kept.Add( entry );
		}

		header.DirectoryOffset = output.Position;

		output.WriteInt( count - 1 );

		var image = new byte[DemoEntry.Size];

		for ( var i = 0; i < count - 1 && i < kept.Count; i++ )
		{
			Array.Clear( image );

			kept[i].Write( image, 0 );

			output.Write( image );
		}

		output.Position = 0;

		header.Write( output );

		Log.Info( "Replacing old demo with edited version.\n" );

		CloseDemoForWrite( output );
	}

	private void SwapDemo( string[] arguments )
	{
		if ( _demoRecording || _demoPlayback )
		{
			Log.Info( "Swapdemo only available when not running or recording a demo.\n" );
			return;
		}

		if ( arguments.Length + 1 != 4 )
		{
			Log.Info( "Swapdemo <demoname> <seg#> <seg#>\nSwaps segments, segment 1 cannot be moved\n" );
			return;
		}

		if ( arguments[0].Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed.\n" );
			return;
		}

		var first = Common.Atoi( arguments[1] );

		if ( first < 2 )
		{
			Log.Info( "Cannot swap the STARTUP segment.\n" );
			return;
		}

		var second = Common.Atoi( arguments[2] );
		var name = Common.DefaultExtension( arguments[0], ".dem" );

		if ( !HasExtension( name, "dem" ) )
		{
			Log.Info( $"Error: couldn't open {name}, demo files must have .dem extension.\n" );
			return;
		}

		Log.Info( $"Swapping segment {first} for {second} from demo {name}.\n" );

		var input = OpenDemoForEdit( name, out var output, out var header );

		if ( input is null )
		{
			Log.Info( $"Error: couldn't open demo file {name} for swapping.\n" );
			return;
		}

		if ( !header.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );
			return;
		}

		if ( !CheckDemoProtocol( header ) )
			return;

		input.Position = header.DirectoryOffset;

		var count = input.ReadInt();

		if ( (uint)(count - 1) >= MaxDemoEntries )
		{
			Log.Info( $"ERROR: demo had bogus # of directory entries:  {count}\n" );
			return;
		}

		if ( second < 1 )
			return;

		if ( first - 1 >= count || second - 1 >= count )
		{
			Log.Info( $"ERROR: demo only has {count} entries\n" );
			return;
		}

		var entries = ReadDirectory( input, count );

		(entries[first - 1], entries[second - 1]) = (entries[second - 1], entries[first - 1]);

		output.Position = header.DirectoryOffset;

		WriteDirectory( output, entries );

		Log.Info( "Replacing old demo with edited version.\n" );

		CloseDemoForWrite( output );
	}

	private void SetDemoInfo( string[] arguments )
	{
		if ( _demoRecording || _demoPlayback )
		{
			Log.Info( "Setdemoinfo only available when not running or recording a demo.\n" );
			return;
		}

		if ( arguments.Length + 1 < 3 )
		{
			Log.Info( "Setdemoinfo <demoname> <seg#> <info ...>\n" );
			Log.Info( "   title \"text\"\n" );
			Log.Info( "   play tracknum\n" );
			Log.Info( "   fade <in | out> <fast | slow>\n\n" );
			Log.Info( "Use -option to disable, e.g., -title\n" );
			return;
		}

		if ( arguments[0].Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed.\n" );
			return;
		}

		var segment = Common.Atoi( arguments[1] );

		if ( segment < 2 )
		{
			Log.Info( "Cannot Setdemoinfo the STARTUP segment.\n" );
			return;
		}

		var name = Common.DefaultExtension( arguments[0], ".dem" );

		if ( !HasExtension( name, "dem" ) )
		{
			Log.Info( $"Error: couldn't open file {name}, demo files must have .dem extension.\n" );
			return;
		}

		Log.Info( $"Setting info for segment {segment} in demo {name}.\n" );

		var input = OpenDemoForEdit( name, out var output, out var header );

		if ( input is null )
		{
			Log.Info( $"Error: couldn't open demo file {name} for Setdemoinfo.\n" );
			return;
		}

		if ( !header.IsDemo )
		{
			Log.Info( $"{name} is not a demo file\n" );
			return;
		}

		if ( !CheckDemoProtocol( header ) )
			return;

		input.Position = header.DirectoryOffset;

		var count = input.ReadInt();

		if ( (uint)(count - 1) >= MaxDemoEntries )
		{
			Log.Info( $"ERROR: demo had bogus # of directory entries:  {count}\n" );
			return;
		}

		var entries = ReadDirectory( input, count );

		if ( segment - 1 >= entries.Count )
			return;

		var entry = entries[segment - 1];
		var index = 2;

		string Argument( int position ) => position < arguments.Length ? arguments[position] : "";

		while ( true )
		{
			var option = Argument( index );

			if ( option.Length == 0 )
				break;

			if ( option.Equals( "-TITLE", StringComparison.OrdinalIgnoreCase ) )
			{
				entry.Flags &= ~DemoEntry.FlagTitle;
				index++;
			}
			else if ( option.Equals( "-PLAY", StringComparison.OrdinalIgnoreCase ) )
			{
				entry.Flags &= ~DemoEntry.FlagPlay;
				index++;
			}
			else if ( option.Equals( "-FADE", StringComparison.OrdinalIgnoreCase ) )
			{
				entry.Flags &= ~(DemoEntry.FlagFadeInSlow | DemoEntry.FlagFadeInFast | DemoEntry.FlagFadeOutSlow | DemoEntry.FlagFadeOutFast);
				index++;
			}
			else if ( option.Equals( "TITLE", StringComparison.OrdinalIgnoreCase ) )
			{
				var title = Argument( index + 1 );

				if ( title.Length == 0 )
				{
					Log.Info( "Title flag requires a double-quoted value.\n" );
				}
				else
				{
					entry.Description = title.Length > DemoEntry.DescriptionSize - 1 ? title[..(DemoEntry.DescriptionSize - 1)] : title;
					entry.Flags |= DemoEntry.FlagTitle;
				}

				index += 2;
			}
			else if ( option.Equals( "PLAY", StringComparison.OrdinalIgnoreCase ) )
			{
				var track = Argument( index + 1 );

				if ( track.Length == 0 )
				{
					Log.Info( "Play flag requires a cd track #.\n" );
				}
				else
				{
					entry.CdTrack = Common.Atoi( track );
					entry.Flags |= DemoEntry.FlagPlay;
				}

				index += 2;
			}
			else if ( option.Equals( "FADE", StringComparison.OrdinalIgnoreCase ) )
			{
				var direction = Argument( index + 1 );

				if ( direction.Length == 0 )
				{
					Log.Info( "Fade flag requires a direction and speed (in fast, e.g.)\n" );

					index += 2;

					continue;
				}

				var fadeIn = direction.Equals( "IN", StringComparison.OrdinalIgnoreCase );

				if ( !fadeIn && !direction.Equals( "OUT", StringComparison.OrdinalIgnoreCase ) )
				{
					Log.Info( "Fade flag requires a direction (in or out).\n" );

					index += 2;

					continue;
				}

				var speed = Argument( index + 2 );

				if ( speed.Equals( "FAST", StringComparison.OrdinalIgnoreCase ) )
					entry.Flags |= fadeIn ? DemoEntry.FlagFadeInFast : DemoEntry.FlagFadeOutFast;
				else if ( speed.Equals( "SLOW", StringComparison.OrdinalIgnoreCase ) )
					entry.Flags |= fadeIn ? DemoEntry.FlagFadeInSlow : DemoEntry.FlagFadeOutSlow;
				else
					Log.Info( "Fade flag requires a speed (fast or slow).\n" );

				index += 3;
			}
			else
			{
				Log.Info( $"Setdemoinfo, unrecognized flag:  {option}\n" );
				break;
			}
		}

		output.Position = header.DirectoryOffset;

		WriteDirectory( output, entries );

		Log.Info( "Replacing old demo with edited version.\n" );

		CloseDemoForWrite( output );
	}

	private void StartDemos( string[] arguments )
	{
		if ( _state == ClientState.Dedicated )
		{
			if ( Server.Get( Scene ) is not { Active: true } )
				Log.Info( "Cannot play demos on a dedicated server.\n" );

			return;
		}

		var count = arguments.Length;

		if ( count > MaxDemos )
		{
			Log.Info( $"Max {MaxDemos} demos in demoloop\n" );

			count = MaxDemos;
		}

		Log.Info( $"{count} demo(s) in loop\n" );

		for ( var i = 0; i < count; i++ )
		{
			var name = arguments[i].Length > MaxDemoName ? arguments[i][..MaxDemoName] : arguments[i];
			var end = name.IndexOfAny( [';', '\n'] );

			_demos[i] = end >= 0 ? name[..end] : name;
		}

		if ( Server.Get( Scene ) is not { Active: true } && _demoNum != -1 && !_demoPlayback )
		{
			_demoNum = 0;

			NextDemo();
		}
		else
		{
			_demoNum = -1;
		}
	}

	private void Demos()
	{
		if ( _state == ClientState.Dedicated )
			return;

		if ( _demoNum == -1 )
			_demoNum = 0;

		DisconnectCommand();
		NextDemo();
	}

	private void StopDemo()
	{
		if ( _state == ClientState.Dedicated || !_demoPlayback )
			return;

		StopPlayback();
		Disconnect();
	}

	[ConCmd( "record" )]
	private static void RecordCommand( params string[] arguments ) => Current?.Record( arguments );

	[ConCmd( "stop" )]
	private static void StopCommand() => Current?.StopRecording();

	[ConCmd( "playdemo" )]
	private static void PlayDemoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.PlayDemo( arguments );
	}

	[ConCmd( "timedemo" )]
	private static void TimeDemoConsoleCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.TimeDemoCommand( arguments );
	}

	[ConCmd( "gg" )]
	private static void GameGaugeConsoleCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.GameGaugeCommand( arguments );
	}

	[ConCmd( "listdemo" )]
	private static void ListDemoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.ListDemo( arguments );
	}

	[ConCmd( "appenddemo" )]
	private static void AppendDemoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.AppendDemo( arguments );
	}

	[ConCmd( "removedemo" )]
	private static void RemoveDemoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.RemoveDemo( arguments );
	}

	[ConCmd( "swapdemo" )]
	private static void SwapDemoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.SwapDemo( arguments );
	}

	[ConCmd( "setdemoinfo" )]
	private static void SetDemoInfoCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
			Current?.SetDemoInfo( arguments );
	}

	[ConCmd( "startdemos" )]
	private static void StartDemosCommand( params string[] arguments ) => Current?.StartDemos( arguments );

	[ConCmd( "demos" )]
	private static void DemosCommand() => Current?.Demos();

	[ConCmd( "stopdemo" )]
	private static void StopDemoCommand() => Current?.StopDemo();
}
