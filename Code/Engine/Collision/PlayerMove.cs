using System;

namespace GoldSrc;

[Flags]
public enum TraceFlags
{
	Normal = 0,
	StudioIgnore = 1,
	StudioBox = 2,
	GlassIgnore = 4,
	WorldOnly = 8
}

public sealed partial class PlayerMove
{
	public const int MaxPhysEnts = 600;
	public const int TraceLinePhysEntsOnly = 0;
	public const int TraceLineAnyVisible = 1;
	public const int MaxMoveEnts = 64;

	public static readonly Vector3[] PlayerMins =
	[
		new( -16f, -16f, -36f ),
		new( -16f, -16f, -18f ),
		new( 0f, 0f, 0f ),
		new( -32f, -32f, -32f )
	];

	public static readonly Vector3[] PlayerMaxs =
	[
		new( 16f, 16f, 36f ),
		new( 16f, 16f, 18f ),
		new( 0f, 0f, 0f ),
		new( 32f, 32f, 32f )
	];

	public Scene Scene;
	public Action<SceneTraceResult> SceneContact;
	public Vector3 OutWishVel;
	public float OutStepHeight;

	public int PlayerIndex;
	public bool Server;
	public bool Multiplayer;
	public float Time;
	public float FrameTime;
	public Vector3 Forward;
	public Vector3 Right;
	public Vector3 Up;

	public Vector3 Origin;
	public Vector3 Angles;
	public Vector3 OldAngles;
	public Vector3 Velocity;
	public Vector3 MoveDir;
	public Vector3 BaseVelocity;

	public Vector3 ViewOffset;
	public float DuckTime;
	public bool InDuck;

	public int TimeStepSound;
	public int StepLeft;
	public float FallVelocity;
	public Vector3 PunchAngle;
	public float SwimTime;
	public float NextPrimaryAttack;
	public int Effects;
	public EntityFlags Flags;
	public int UseHull;
	public float Gravity;
	public float Friction;
	public int OldButtons;
	public float WaterJumpTime;
	public bool Dead;
	public int DeadFlag;
	public int Spectator;
	public MoveType MoveType;
	public int OnGround;
	public int WaterLevel;
	public int WaterType;
	public int OldWaterLevel;
	public string TextureName = string.Empty;
	public char TextureType;
	public float MaxSpeed;
	public float ClientMaxSpeed;

	public int User1;
	public int User2;
	public int User3;
	public int User4;
	public float FloatUser1;
	public float FloatUser2;
	public float FloatUser3;
	public float FloatUser4;
	public Vector3 VectorUser1;
	public Vector3 VectorUser2;
	public Vector3 VectorUser3;
	public Vector3 VectorUser4;

	public readonly List<PhysEnt> PhysEnts = [];
	public readonly List<PhysEnt> MoveEnts = [];
	public readonly List<PhysEnt> VisEnts = [];

	public UserCmd Cmd;

	public readonly List<PmTrace> TouchIndex = [];

	public string PhysInfo = "";

	public MoveVars MoveVars;
	public bool RunFuncs;

	public Action<SoundChannel, string, float, float, int, int> PlaySound;
	public Func<int, Vector3, Vector3, string> TraceTexture;
	public Func<string, string> LoadFile;

	public void StuckTouch( int hitEnt, PmTrace trace )
	{
		if ( !Server )
			return;

		foreach ( var touched in TouchIndex )
		{
			if ( touched.Ent == trace.Ent )
				return;
		}

		trace.DeltaVelocity = Velocity;

		if ( TouchIndex.Count >= MaxPhysEnts )
			Con.DPrintf( "Too many entities were touched!\n" );

		TouchIndex.Add( trace );
	}

	public int RandomLong( int low, int high ) => GoldSrc.Server.RandomLong( low, high );

	public string InfoValueForKey( string key )
	{
		return Info.ValueForKey( PhysInfo, key );
	}

	public double FloatTime() => RealTime.Now;
}
