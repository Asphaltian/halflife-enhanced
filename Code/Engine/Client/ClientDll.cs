using System;

namespace GoldSrc;

public struct RefParams
{
	public Vector3 ViewOrigin;
	public Vector3 ViewAngles;
	public float FrameTime;
	public float Time;
	public bool Paused;
	public bool Intermission;
	public bool NoClip;
	public float IdealPitch;
	public bool OnGround;
	public int WaterLevel;
	public Vector3 SimVelocity;
	public Vector3 SimOrigin;
	public Vector3 ViewHeight;
	public Vector3 ClientViewAngles;
	public int Health;
	public Vector3 PunchAngle;
	public int MaxClients;
	public bool Smoothing;
	public MoveVars MoveVars;
	public Vector3 EntityAngles;
	public Func<Vector3, int> PointContents;
	public Func<Vector3, float?> WaterEntityScale;
	public Vector3 CrosshairAngle;
	public float ViewSize;
	public Vector3 ViewModelOrigin;
	public Vector3 ViewModelAngles;
	public Vector3 Forward;
	public Vector3 Right;
	public Vector3 Up;
	public bool Spectator;
	public int ViewEntity;
	public int PlayerNum;
	public int MaxEntities;
	public bool DemoPlayback;
	public int Hardware;
	public UserCmd Cmd;
	public int ViewportX;
	public int ViewportY;
	public int ViewportWidth;
	public int ViewportHeight;
	public int NextView;
	public int OnlyClientDraw;
}

public abstract class ClientDll
{
	public delegate bool UserMsgHook( string name, byte[] data );

	private readonly Dictionary<string, UserMsgHook> _userMsgHooks = [];
	private readonly Dictionary<string, Action<EventArgs>> _eventHooks = new( StringComparer.OrdinalIgnoreCase );

	public Client Engine { get; set; }

	public abstract void Init();

	public abstract bool KeyCommand( string name, bool down );

	public abstract bool KeyEvent( bool down, int keyNum, string currentBinding );

	public abstract bool MouseEvent();

	public abstract int? FindKey( string name );

	public abstract bool IsThirdPerson();

	public abstract void CamThink();

	public abstract void DirectorMessage( byte[] data );

	public abstract void ChatInputPosition( ref int x, ref int y );

	public abstract void PlayerMoveInit( PlayerMove pmove );

	public abstract void StudioEvent( in Studio.Event studioEvent, Vector3[] attachments );

	public abstract void TempEntUpdate( double frameTime, double clientTime, double gravity, ref TempEnt free, ref TempEnt active, Func<TempEnt, bool> addVisibleEntity, Action<TempEnt, float> playSound );

	public void HookUserMsg( string name, UserMsgHook hook ) => _userMsgHooks[name] = hook;

	protected void HookEvent( string name, Action<EventArgs> hook ) => _eventHooks[name] = hook;

	public void FireEvent( in QueuedEvent queued )
	{
		if ( _eventHooks.TryGetValue( queued.Name, out var hook ) )
			hook( queued.Args );
		else
			Log.Info( $"CL_FireEvents:  Unknown event {queued.Index}:{queued.Name}" );
	}

	public void DispatchUserMsg( UserMessage message, byte[] data )
	{
		if ( !DispatchUserMsg( message.Name, data ) )
			Log.Trace( $"UserMsg: No pfn {message.Name} {message.Id}\n" );
	}

	public bool DispatchUserMsg( string name, byte[] data )
	{
		if ( !_userMsgHooks.TryGetValue( name, out var hook ) )
			return false;

		hook( name, data );

		return true;
	}

	public abstract void MouseMove( ref Vector3 viewAngles );

	public abstract void CreateMove( float frameTime, ref UserCmd cmd, ref Vector3 viewAngles, bool active );

	public abstract void PlayerMove( PlayerMove pmove, bool server );

	public abstract void PostRunCmd( LocalState from, LocalState to, ref UserCmd cmd, bool runFuncs, double time, uint randomSeed );

	public abstract void ProcessPlayerState( ref EntityState destination, in EntityState source );

	public abstract void TxferLocalOverrides( ref EntityState state, in LocalClientData client );

	public abstract void TxferPredictionData( ref EntityState playerState, in EntityState predictedPlayerState, ref LocalClientData clientData, in LocalClientData predictedClientData, WeaponData[] weaponData, WeaponData[] predictedWeaponData );

	public abstract void CalcRefdef( ref RefParams refParams );

	public const int EntityTypeNormal = 0;
	public const int EntityTypePlayer = 1;
	public const int EntityTypeTempEntity = 2;
	public const int EntityTypeBeam = 3;
	public const int EntityTypeFragmented = 4;

	public abstract bool AddEntity( int type, ClientEntity entity, int index, string modelName );

	public abstract void CreateEntities();

	public virtual void Frame( double time )
	{
	}

	public virtual GameClientExports GameClientExports => null;

	public virtual void VoiceStatus( int entityIndex, bool talking )
	{
	}

	public virtual void DrawNormalTriangles()
	{
	}

	public virtual void DemoReadBuffer( int size, byte[] buffer )
	{
	}

	public virtual void DrawTransparentTriangles()
	{
	}

	public abstract void VidInit();

	public abstract bool UpdateClientData( ref ClientData data, float time );

	public abstract void Redraw( float time, bool intermission );

	private static ClientDll _loaded;

	public static ClientDll Loaded => _loaded;

	public static ClientDll Load( Client engine )
	{
		foreach ( var type in Game.TypeLibrary.GetTypes<ClientDll>() )
		{
			if ( type.IsAbstract )
				continue;

			var dll = type.Create<ClientDll>();

			Unload();

			_loaded = dll;

			dll.Engine = engine;
			dll.Init();

			return dll;
		}

		throw new InvalidOperationException( "Unable to load client DLL" );
	}

	public static void Unload( Client engine )
	{
		if ( _loaded?.Engine == engine )
			Unload();
	}

	public virtual void Shutdown()
	{
	}

	private static void Unload()
	{
		var dll = _loaded;

		_loaded = null;

		dll?.Shutdown();
	}
}
