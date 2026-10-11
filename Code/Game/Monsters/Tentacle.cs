using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_tentacle" )]
public class Tentacle : BaseMonster
{
	private const int ActivityIdle = 1010;
	private const int ActivityTap = 1020;
	private const int ActivityStrike = 1030;
	private const int ActivityRearIdle = 1040;

	private const int TapNone = -1;
	private const int TapSilo = 0;
	private const int TapDirt = 1;
	private const int TapWater = 2;

	private const int AnimPitIdle = 0;
	private const int AnimRiseToTemp1 = 1;
	private const int AnimTemp1ToFloor = 2;
	private const int AnimFloorIdle = 3;
	private const int AnimFloorTap = 8;
	private const int AnimFloorRear = 10;
	private const int AnimFloorRearIdle = 11;
	private const int AnimFloorToLev1 = 12;
	private const int AnimLev1Idle = 13;
	private const int AnimLev1Tap = 18;
	private const int AnimLev1Rear = 20;
	private const int AnimLev1RearIdle = 21;
	private const int AnimLev1ToLev2 = 22;
	private const int AnimLev2Idle = 23;
	private const int AnimLev2Tap = 28;
	private const int AnimLev2Rear = 30;
	private const int AnimLev2RearIdle = 31;
	private const int AnimLev2ToLev3 = 32;
	private const int AnimLev3Idle = 33;
	private const int AnimLev3Tap = 38;
	private const int AnimLev3Rear = 40;
	private const int AnimLev3RearIdle = 41;
	private const int AnimEngineIdle = 44;
	private const int AnimEngineSway = 45;
	private const int AnimEngineBob = 47;
	private const int AnimEngineDeath1 = 48;
	private const int AnimEngineDeath3 = 50;

	private static readonly string[] HitSilo = ["tentacle/te_strike1.wav", "tentacle/te_strike2.wav"];
	private static readonly string[] HitDirt = ["player/pl_dirt1.wav", "player/pl_dirt2.wav", "player/pl_dirt3.wav", "player/pl_dirt4.wav"];
	private static readonly string[] HitWater = ["player/pl_slosh1.wav", "player/pl_slosh2.wav", "player/pl_slosh3.wav", "player/pl_slosh4.wav"];

	private static bool _flySound;
	private static bool _squirmSound;

	[Save( FieldType.Float )]
	private float _initialYaw;
	[Save( FieldType.Integer )]
	private int _goalAnim;
	[Save( FieldType.Integer )]
	private int _direction;
	[Save( FieldType.Float )]
	private float _frameRateAdjust;
	[Save( FieldType.Float )]
	private float _soundYaw;
	[Save( FieldType.Integer )]
	private int _soundLevel;
	[Save( FieldType.Time )]
	private float _soundTime;
	[Save( FieldType.Integer )]
	private int _hitDamage;
	[Save( FieldType.Time )]
	private float _hitTime;
	[Save( FieldType.Float )]
	private float _tapRadius;
	[Save( FieldType.Time )]
	private float _nextSong;
	[Save( FieldType.Integer )]
	private int _tapSound;
	[Save( FieldType.Float )]
	private float _maxYaw = 65f;
	[Save( FieldType.PositionVector )]
	private Vector3 _previousSound;
	[Save( FieldType.Time )]
	private float _previousSoundTime;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override float HearingSensitivity => 2f;

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -400f, -400f, 0f );
		AbsMax = Origin + new Vector3( 400f, 400f, 850f );
	}

	public override MonsterState GetIdealState() => MonsterState.Idle;

	public override int Classify() => ClassAlienMonster;

	public override void Spawn()
	{
		Precache();

		Solid = Solid.BBox;
		MoveType = MoveType.Fly;
		Effects = 0;
		Health = 75f;
		Sequence = 0;

		SetModel( "models/tentacle2.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		DamageMode = DamageAim;
		EntityFlags |= EntityFlags.Monster;

		MonsterBloodColor = BloodColorGreen;

		SetThink( Start );
		SetTouch( HitTouch );
		SetUse( CommandUse );

		NextThink = (float)(Server.Globals.Time + 0.2);

		ResetSequenceInfo();
		_direction = 1;

		YawSpeed = 18f;
		_initialYaw = Angles.y;
		IdealYaw = _initialYaw;

		_flySound = false;
		_squirmSound = false;

		_hitDamage = 20;

		if ( _maxYaw <= 0f )
			_maxYaw = 65f;

		MonsterState = MonsterState.Idle;

		SetOrigin( Origin );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/tentacle2.mdl" );

		Server.PrecacheSound( "ambience/flies.wav" );
		Server.PrecacheSound( "ambience/squirm2.wav" );

		Server.PrecacheSound( "tentacle/te_alert1.wav" );
		Server.PrecacheSound( "tentacle/te_alert2.wav" );
		Server.PrecacheSound( "tentacle/te_flies1.wav" );
		Server.PrecacheSound( "tentacle/te_move1.wav" );
		Server.PrecacheSound( "tentacle/te_move2.wav" );
		Server.PrecacheSound( "tentacle/te_roar1.wav" );
		Server.PrecacheSound( "tentacle/te_roar2.wav" );
		Server.PrecacheSound( "tentacle/te_search1.wav" );
		Server.PrecacheSound( "tentacle/te_search2.wav" );
		Server.PrecacheSound( "tentacle/te_sing1.wav" );
		Server.PrecacheSound( "tentacle/te_sing2.wav" );
		Server.PrecacheSound( "tentacle/te_squirm2.wav" );
		Server.PrecacheSound( "tentacle/te_strike1.wav" );
		Server.PrecacheSound( "tentacle/te_strike2.wav" );
		Server.PrecacheSound( "tentacle/te_swing1.wav" );
		Server.PrecacheSound( "tentacle/te_swing2.wav" );

		PrecacheSounds( HitSilo );
		PrecacheSounds( HitDirt );
		PrecacheSounds( HitWater );
	}

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "sweeparc":
				_maxYaw = (float)(Util.Atof( value ) / 2.0);
				return true;
			case "sound":
				_tapSound = Util.Atoi( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public static int Level( float height )
	{
		if ( height < 216f )
			return 0;

		if ( height < 408f )
			return 1;

		return height < 600f ? 2 : 3;
	}

	public float MyHeight()
	{
		return MyLevel() switch
		{
			1 => 256f,
			2 => 448f,
			3 => 640f,
			_ => 0f
		};
	}

	public int MyLevel()
	{
		return Sequence switch
		{
			AnimPitIdle => -1,
			AnimRiseToTemp1 or AnimTemp1ToFloor or AnimFloorToLev1 => 0,
			>= AnimFloorIdle and <= AnimFloorRearIdle => 0,
			>= AnimLev1Idle and <= AnimLev1ToLev2 => 1,
			>= AnimLev2Idle and <= AnimLev2ToLev3 => 2,
			>= AnimLev3Idle and <= AnimLev3RearIdle => 3,
			_ => -1
		};
	}

	public void Test()
	{
		Sequence = 7;
		FrameRate = 0f;
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	private bool IsRearSequence()
	{
		return Sequence is AnimFloorRear or AnimFloorRearIdle or AnimLev1Rear or AnimLev1RearIdle or AnimLev2Rear or AnimLev2RearIdle or AnimLev3Rear or AnimLev3RearIdle;
	}

	private void ChooseGoalAnim()
	{
		var time = Server.Globals.Time;

		if ( Health <= 1f )
		{
			_goalAnim = AnimPitIdle;

			if ( Sequence == AnimPitIdle )
				Health = 75f;
		}
		else if ( _soundTime > time )
		{
			if ( _soundYaw >= -(_maxYaw + 30f) && _soundYaw <= _maxYaw + 30f )
				_goalAnim = LookupActivity( ActivityStrike + _soundLevel );
			else if ( _soundYaw >= -_maxYaw * 2f && _soundYaw <= _maxYaw * 2f )
				_goalAnim = LookupActivity( ActivityTap + _soundLevel );
			else
				_goalAnim = LookupActivity( ActivityRearIdle + _soundLevel );
		}
		else if ( Sequence == AnimPitIdle )
		{
			_goalAnim = AnimPitIdle;
		}
		else if ( Sequence == _goalAnim )
		{
			if ( MyLevel() >= 0 && time < _soundTime )
			{
				_goalAnim = LookupActivity( (Server.RandomLong( 0, 9 ) < _soundTime - time ? ActivityStrike : ActivityTap) + _soundLevel );
			}
			else if ( MyLevel() < 0 )
			{
				_goalAnim = LookupActivity( ActivityIdle + 0 );
			}
			else
			{
				if ( _nextSong < time )
				{
					Sound.Emit( this, SoundChannel.Voice, Server.RandomLong( 0, 1 ) == 0 ? "tentacle/te_sing1.wav" : "tentacle/te_sing2.wav", 1f, Server.AttenuationNormal );

					_nextSong = time + Server.RandomFloat( 10f, 20f );
				}

				if ( Server.RandomLong( 0, 15 ) == 0 )
					_goalAnim = LookupActivity( ActivityIdle + Server.RandomLong( 0, 3 ) );
				else if ( Server.RandomLong( 0, 3 ) == 0 )
					_goalAnim = LookupActivity( ActivityTap + MyLevel() );
				else
					_goalAnim = LookupActivity( ActivityIdle + MyLevel() );
			}

			if ( _soundYaw < 0f )
				_soundYaw += Server.RandomFloat( 2f, 8f );
			else
				_soundYaw -= Server.RandomFloat( 2f, 8f );
		}
	}

	public void Cycle()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);

		if ( MonsterState == MonsterState.Script || IdealMonsterState == MonsterState.Script )
		{
			Angles = Angles with { y = _initialYaw };
			IdealYaw = _initialYaw;

			ClearConditions( IgnoreConditions() );
			MonsterThink();

			_goalAnim = AnimPitIdle;
			return;
		}

		DispatchAnimEvents();
		StudioFrameAdvance();

		ChangeYaw( (int)YawSpeed );

		Listen();

		if ( HasConditions( ConditionHearSound ) && BestSound() is { } sound )
		{
			Vector3 direction;

			if ( globals.Time - _previousSoundTime < 0.5 )
			{
				var deltaTime = globals.Time - _previousSoundTime;

				direction = sound.Origin + ((sound.Origin - _previousSound) / deltaTime) - Origin;
			}
			else
			{
				direction = sound.Origin - Origin;
			}

			_previousSoundTime = globals.Time;
			_previousSound = sound.Origin;

			_soundYaw = MathLib.VecToYaw( direction ) - _initialYaw;
			_soundLevel = Level( direction.z );

			if ( _soundYaw < -180f )
				_soundYaw += 360f;

			if ( _soundYaw > 180f )
				_soundYaw -= 360f;

			if ( _soundTime < globals.Time )
				Server.RandomLong( 0, 1 );

			_soundTime = globals.Time + Server.RandomFloat( 5f, 10f );
		}

		var yawDelta = _soundYaw;

		if ( IsRearSequence() )
		{
			if ( yawDelta < 0f && yawDelta > -_maxYaw )
				yawDelta = -_maxYaw;

			if ( yawDelta > 0f && yawDelta < _maxYaw )
				yawDelta = _maxYaw;
		}
		else
		{
			if ( yawDelta < -_maxYaw )
				yawDelta = -_maxYaw;

			if ( yawDelta > _maxYaw )
				yawDelta = _maxYaw;
		}

		IdealYaw = _initialYaw + yawDelta;

		if ( SequenceFinished )
		{
			ChooseGoalAnim();

			Sequence = FindTransition( Sequence, _goalAnim, ref _direction );

			if ( _direction > 0 )
			{
				Frame = 0f;
			}
			else
			{
				_direction = -1;
				Frame = 255f;
			}

			ResetSequenceInfo();

			_frameRateAdjust = Server.RandomFloat( -0.2f, 0.2f );
			FrameRate = (float)((_direction * 1.0) + _frameRateAdjust);

			if ( Sequence is AnimFloorTap or AnimLev1Tap or AnimLev2Tap or AnimLev3Tap )
			{
				globals.MakeVectors( Angles );

				var source = Origin + new Vector3( 0f, 0f, MyHeight() - 4f );
				var low = Util.TraceLine( source, source + (globals.Forward * 512f), IgnoreMonsters, this );

				source = Origin + new Vector3( 0f, 0f, MyHeight() + 8f );

				var high = Util.TraceLine( source, source + (globals.Forward * 512f), IgnoreMonsters, this );

				_tapRadius = SetBlending( 0, Server.RandomFloat( low.Fraction * 512f, high.Fraction * 512f ) );
			}
			else
			{
				_tapRadius = 336f;
			}

			ViewOffset = ViewOffset with { z = MyHeight() };
		}

		if ( _previousSoundTime + 2.0 > globals.Time )
			FrameRate = (float)((_direction * 1.5) + _frameRateAdjust);
		else if ( _previousSoundTime + 5.0 > globals.Time )
			FrameRate = _direction + (_direction * (5f - (globals.Time - _previousSoundTime)) / 2f) + _frameRateAdjust;
	}

	public void CommandUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		switch ( useType )
		{
			case UseType.Off:
				DamageMode = DamageNo;
				SetThink( DieThink );
				_goalAnim = AnimEngineDeath1;
				break;

			case UseType.On:
				if ( activator is not null )
					SoundEnt.InsertSound( WorldSound.World, activator.Origin, 1024, 1f );

				break;

			case UseType.Set:
				break;

			case UseType.Toggle:
				DamageMode = DamageNo;
				SetThink( DieThink );
				_goalAnim = AnimEngineIdle;
				break;
		}
	}

	public void DieThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		DispatchAnimEvents();
		StudioFrameAdvance();

		ChangeYaw( 24 );

		if ( !SequenceFinished )
			return;

		if ( Sequence == _goalAnim )
		{
			switch ( _goalAnim )
			{
				case >= AnimEngineIdle and <= AnimEngineBob:
					_goalAnim = AnimEngineSway + Server.RandomLong( 0, 2 );
					break;

				case >= AnimEngineDeath1 and <= AnimEngineDeath3:
					Remove();
					return;
			}
		}

		Sequence = FindTransition( Sequence, _goalAnim, ref _direction );
		Frame = _direction > 0 ? 0f : 255f;

		ResetSequenceInfo();

		float yawDelta;

		if ( IsRearSequence() || Sequence is >= AnimEngineIdle and <= AnimEngineDeath3 )
		{
			FrameRate = Server.RandomFloat( (float)(_direction - 0.2), (float)(_direction + 0.2) );
			yawDelta = 180f;
		}
		else
		{
			FrameRate = 1.5f;
			yawDelta = 0f;
		}

		IdealYaw = _initialYaw + yawDelta;
	}

	private void TapSound( Vector3 source, float volume )
	{
		switch ( _tapSound )
		{
			case TapSilo:
				Sound.EmitAmbient( this, source, RandomSound( HitSilo ), volume, Server.AttenuationNormal, 0, 100 );
				break;

			case TapNone:
				break;

			case TapDirt:
				Sound.EmitAmbient( this, source, RandomSound( HitDirt ), volume, Server.AttenuationNormal, 0, 100 );
				break;

			case TapWater:
				Sound.EmitAmbient( this, source, RandomSound( HitWater ), volume, Server.AttenuationNormal, 0, 100 );
				break;
		}
	}

	private void HeightSound( string first, string second )
	{
		var sound = Server.RandomLong( 0, 1 ) == 0 ? first : second;

		Sound.EmitAmbient( this, Origin + new Vector3( 0f, 0f, MyHeight() ), sound, 1f, Server.AttenuationNormal, 0, 100 );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case 1:
				TapSound( Server.GetAttachment( this, 0 ), 1f );

				Server.Globals.ForceRetouch++;
				break;

			case 3:
				_hitDamage = 200;
				break;

			case 4:
				_hitDamage = 25;
				break;

			case 5:
				break;

			case 2:
			case 6:
				{
					var yaw = Angles.y * (Math.PI / 180.0);
					var source = Origin + (_tapRadius * new Vector3( (float)Math.Cos( yaw ), (float)Math.Sin( yaw ), 0f ));

					source.z += MyHeight();

					TapSound( source, Server.RandomFloat( 0.3f, 0.5f ) );
					break;
				}

			case 7:
				HeightSound( "tentacle/te_roar1.wav", "tentacle/te_roar2.wav" );
				break;

			case 8:
				HeightSound( "tentacle/te_search1.wav", "tentacle/te_search2.wav" );
				break;

			case 9:
				HeightSound( "tentacle/te_move1.wav", "tentacle/te_move2.wav" );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public void Start()
	{
		SetThink( Cycle );

		if ( !_flySound )
		{
			Sound.Emit( this, SoundChannel.Body, "ambience/flies.wav", 1f, Server.AttenuationNormal );
			_flySound = true;
		}
		else if ( !_squirmSound )
		{
			Sound.Emit( this, SoundChannel.Body, "ambience/squirm2.wav", 1f, Server.AttenuationNormal );
			_squirmSound = true;
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void HitTouch( BaseEntity other )
	{
		var trace = Server.Globals.Trace;

		if ( other.ModelIndex == ModelIndex )
			return;

		if ( _hitTime > Server.Globals.Time )
			return;

		if ( trace.Ent is null || trace.Ent.ModelIndex != ModelIndex )
			return;

		if ( trace.HitGroup >= 3 )
			other.TakeDamage( this, this, _hitDamage, DamageCrush );
		else if ( trace.HitGroup != 0 )
			other.TakeDamage( this, this, 20f, DamageCrush );
		else
			return;

		_hitTime = (float)(Server.Globals.Time + 0.5);
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( damage > Health )
			Health = 1f;
		else
			Health -= damage;

		return true;
	}

	public override void Killed( BaseEntity attacker, int gib ) => _goalAnim = AnimPitIdle;
}

[LinkEntityToClass( "monster_tentaclemaw" )]
public class TentacleMaw : BaseMonster
{
	public override void Spawn()
	{
		Precache();

		SetModel( "models/maw.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.Not;
		MoveType = MoveType.Step;
		Effects = 0;
		Health = 75f;
		YawSpeed = 8f;
		Sequence = 0;

		Angles = Angles with { x = 90f };
	}

	public override void Precache() => Server.PrecacheModel( "models/maw.mdl" );
}
