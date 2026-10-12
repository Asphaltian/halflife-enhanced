using System;
using GoldSrc;

namespace HalfLife;

public static class GargTask
{
	public const int SoundAttack = TaskId.LastCommon + 1;
	public const int FlameSweep = TaskId.LastCommon + 2;
}

[LinkEntityToClass( "streak_spiral" )]
public class Spiral : BaseEntity
{
	private const float Interval = 0.1f;

	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;

	public override void Spawn()
	{
		MoveType = MoveType.None;
		NextThink = Server.Globals.Time;
		Solid = Solid.Not;
		SetSize( Vector3.Zero, Vector3.Zero );
		Effects |= Effects.NoDraw;
		Angles = Vector3.Zero;
	}

	public static Spiral Create( Server server, Vector3 origin, float height, float radius, float duration )
	{
		if ( duration <= 0f )
			return null;

		var spiral = server.CreateEntity<Spiral>();

		spiral.Spawn();
		spiral.DamageTime = spiral.NextThink;
		spiral.Origin = origin;
		spiral.Scale = radius;
		spiral.Damage = height;
		spiral.Speed = duration;
		spiral.Health = 0f;
		spiral.Angles = Vector3.Zero;

		return spiral;
	}

	public override void Think()
	{
		var time = Server.Globals.Time - DamageTime;

		while ( time > Interval )
		{
			var position = Origin;
			var fraction = 1f / Speed;
			var radius = Scale * Health * fraction;

			position.z += Health * Damage * fraction;
			Angles = Angles with { y = Health * 360f * 8f * fraction };
			Server.Globals.MakeVectors( Angles );
			position += Server.Globals.Forward * radius;

			Util.StreakSplash( position, new Vector3( 0f, 0f, 1f ), Server.RandomLong( 8, 11 ), 20, Server.RandomLong( 50, 150 ), 400 );

			DamageTime += Interval;
			Health += Interval;
			time -= Interval;
		}

		NextThink = Server.Globals.Time;

		if ( Health >= Speed )
			Remove();
	}
}

[LinkEntityToClass( "garg_stomp" )]
public class Stomp : BaseEntity
{
	private const float Interval = 0.025f;

	public static Stomp StompCreate( Server server, Vector3 origin, Vector3 end, float speed )
	{
		var stomp = server.CreateEntity<Stomp>();
		var direction = end - origin;

		stomp.Origin = origin;
		stomp.Scale = MathLib.Length( direction );
		stomp.MoveDir = Util.Normalize( direction );
		stomp.Speed = speed;
		stomp.Spawn();

		return stomp;
	}

	public override void Spawn()
	{
		NextThink = Server.Globals.Time;
		ClassName = "garg_stomp";
		DamageTime = Server.Globals.Time;

		FrameRate = 30f;
		ModelName = Gargantua.StompSpriteName;
		RenderMode = RenderMode.TransTexture;
		RenderAmt = 0f;
		Sound.EmitDyn( this, SoundChannel.Body, Gargantua.StompBuzzSound, 1f, Server.AttenuationNormal, 0, (int)(Server.PitchNormal * 0.55) );
	}

	public override void Think()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);

		var start = Origin;

		start.z += 30f;

		var end = start + (MoveDir * Speed * globals.FrameTime);
		var trace = Util.TraceHull( start, end, DontIgnoreMonsters, HeadHull, this );

		if ( trace.Ent is BaseEntity entity && trace.Ent != Owner )
			entity.TakeDamage( this, Owner as BaseEntity ?? this, HalfLifeDll.SkillData.GargantuaDmgStomp, DamageSonic );

		Speed += globals.FrameTime * FrameRate;
		FrameRate += globals.FrameTime * 1500f;

		while ( globals.Time - DamageTime > Interval )
		{
			Origin += MoveDir * Speed * Interval;

			for ( var i = 0; i < 2; i++ )
			{
				var sprite = Sprite.SpriteCreate( Server, Gargantua.StompSpriteName, Origin, true );

				trace = Util.TraceLine( Origin, Origin - new Vector3( 0f, 0f, 500f ), IgnoreMonsters, this );
				sprite.Origin = trace.EndPos;
				sprite.Velocity = new Vector3( Server.RandomFloat( -200f, 200f ), Server.RandomFloat( -200f, 200f ), 175f );
				sprite.NextThink = (float)(globals.Time + 0.3);
				sprite.SetThink( sprite.SubRemove );
				sprite.SetTransparency( RenderMode.TransAdd, 255, 255, 255, 255, RenderFx.FadeFast );
			}

			DamageTime += Interval;
			Scale -= Interval * Speed;

			if ( Scale <= 0f )
			{
				Remove();
				Sound.Stop( this, SoundChannel.Body, Gargantua.StompBuzzSound );
			}
		}
	}
}

[LinkEntityToClass( "env_smoker" )]
public class Smoker : BaseEntity
{
	public override void Spawn()
	{
		MoveType = MoveType.None;
		NextThink = Server.Globals.Time;
		Solid = Solid.Not;
		SetSize( Vector3.Zero, Vector3.Zero );
		Effects |= Effects.NoDraw;
		Angles = Vector3.Zero;
	}

	public override void Think()
	{
		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.Smoke );
		Server.WriteCoord( Origin.x + Server.RandomFloat( -Damage, Damage ) );
		Server.WriteCoord( Origin.y + Server.RandomFloat( -Damage, Damage ) );
		Server.WriteCoord( Origin.z );
		Server.WriteShort( GameModels.Smoke );
		Server.WriteByte( Server.RandomLong( (int)Scale, (int)(Scale * 1.1) ) );
		Server.WriteByte( Server.RandomLong( 8, 14 ) );
		Server.MessageEnd();

		Health--;

		if ( Health > 0f )
			NextThink = Server.Globals.Time + Server.RandomFloat( 0.1f, 0.2f );
		else
			Remove();
	}
}

[SkipHotload]
public static class GargantuaAi
{
	public static readonly Schedule GargFlame = new( "GargFlame",
		0,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( GargTask.SoundAttack ),
		new( TaskId.SetActivity, (float)Activity.MeleeAttack2 ),
		new( GargTask.FlameSweep, 4.5f ),
		new( TaskId.SetActivity, (float)Activity.Idle ) );

	public static readonly Schedule GargSwipe = new( "GargSwipe",
		BaseMonster.ConditionCanMeleeAttack2,
		0,
		new( TaskId.StopMoving ),
		new( TaskId.FaceEnemy ),
		new( TaskId.MeleeAttack1 ) );

	public static readonly Schedule[] List =
	[
		GargFlame,
		GargSwipe
	];
}

[LinkEntityToClass( "monster_gargantua" )]
public class Gargantua : BaseMonster
{
	public const string StompSpriteName = "sprites/gargeye1.spr";
	public const string StompBuzzSound = "weapons/mine_charge.wav";

	private const float AttackDistance = 80f;

	private const int EventSlashLeft = 1;
	private const int EventLeftFoot = 3;
	private const int EventRightFoot = 4;
	private const int EventStomp = 5;
	private const int EventBreathe = 6;

	private const int GargDamage = DamageEnergyBeam | DamageCrush | DamageMortar | DamageBlast;
	private const string EyeSpriteName = "sprites/gargeye1.spr";
	private const string BeamSpriteName = "sprites/xbeam3.spr";
	private const string BeamSprite2 = "sprites/xbeam3.spr";
	private const int FlameLength = 330;
	private const string GibModel = "models/metalplategibs.mdl";

	private static readonly string[] AttackHitSounds = ["zombie/claw_strike1.wav", "zombie/claw_strike2.wav", "zombie/claw_strike3.wav"];
	private static readonly string[] BeamAttackSounds = ["garg/gar_flameoff1.wav", "garg/gar_flameon1.wav", "garg/gar_flamerun1.wav"];
	private static readonly string[] AttackMissSounds = ["zombie/claw_miss1.wav", "zombie/claw_miss2.wav"];
	private static readonly string[] RicSounds = ["debris/metal4.wav", "debris/metal6.wav", "weapons/ric4.wav", "weapons/ric5.wav"];
	private static readonly string[] FootSounds = ["garg/gar_step1.wav", "garg/gar_step2.wav"];
	private static readonly string[] IdleSounds = ["garg/gar_idle1.wav", "garg/gar_idle2.wav", "garg/gar_idle3.wav", "garg/gar_idle4.wav", "garg/gar_idle5.wav"];
	private static readonly string[] AttackSounds = ["garg/gar_attack1.wav", "garg/gar_attack2.wav", "garg/gar_attack3.wav"];
	private static readonly string[] AlertSounds = ["garg/gar_alert1.wav", "garg/gar_alert2.wav", "garg/gar_alert3.wav"];
	private static readonly string[] PainSounds = ["garg/gar_pain1.wav", "garg/gar_pain2.wav", "garg/gar_pain3.wav"];
	private static readonly string[] StompSounds = ["garg/gar_stomp1.wav"];
	private static readonly string[] BreatheSounds = ["garg/gar_breathe1.wav", "garg/gar_breathe2.wav", "garg/gar_breathe3.wav"];

	[Save( FieldType.ClassPtr )]
	private Sprite _eyeGlow;
	[Save( FieldType.ClassPtr )]
	private readonly Beam[] _flame = new Beam[4];

	[Save( FieldType.Integer )]
	private int _eyeBrightness;
	[Save( FieldType.Time )]
	private float _seeTime;
	[Save( FieldType.Time )]
	private float _flameTime;
	[Save( FieldType.Time )]
	private float _painSoundTime;
	[Save( FieldType.Time )]
	private float _streakTime;
	[Save( FieldType.Float )]
	private float _flameX;
	[Save( FieldType.Float )]
	private float _flameY;

	private int _gibModel;

	private bool FlameIsOn => _flame[0] is not null;

	public override Schedule ScheduleFromName( string name ) => ScheduleInList( name, GargantuaAi.List ) ?? base.ScheduleFromName( name );

	public override int Classify() => ClassAlienMonster;

	public override void SetYawSpeed()
	{
		YawSpeed = Activity switch
		{
			Activity.TurnLeft or Activity.TurnRight => 180f,
			_ => 60f
		};
	}

	public override void SetObjectCollisionBox()
	{
		AbsMin = Origin + new Vector3( -80f, -80f, 0f );
		AbsMax = Origin + new Vector3( 80f, 80f, 214f );
	}

	private void EyeOn( int level ) => _eyeBrightness = level;

	private void EyeOff() => _eyeBrightness = 0;

	private void EyeUpdate()
	{
		if ( _eyeGlow is null )
			return;

		_eyeGlow.RenderAmt = Util.Approach( _eyeBrightness, _eyeGlow.RenderAmt, 26f );

		if ( _eyeGlow.RenderAmt == 0f )
			_eyeGlow.Effects |= Effects.NoDraw;
		else
			_eyeGlow.Effects &= ~Effects.NoDraw;

		Server.SetOrigin( _eyeGlow, Origin );
	}

	private void StompAttack()
	{
		Server.Globals.MakeVectors( Angles );

		var start = Origin + new Vector3( 0f, 0f, 60f ) + (35f * Server.Globals.Forward);
		var aim = ShootAtEnemy( start );
		var end = (aim * 1024f) + start;

		var trace = Util.TraceLine( start, end, IgnoreMonsters, this );

		Stomp.StompCreate( Server, start, trace.EndPos, 0f );
		Util.ScreenShake( Origin, 12f, 100f, 2f, 1000f );
		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( StompSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal + Server.RandomLong( -10, 10 ) );

		trace = Util.TraceLine( Origin, Origin - new Vector3( 0f, 0f, 20f ), IgnoreMonsters, this );

		if ( trace.Fraction < 1.0 )
			Util.DecalTrace( trace, Decals.GargStomp1 );
	}

	private void FlameCreate()
	{
		Server.Globals.MakeVectors( Angles );

		for ( var i = 0; i < 4; i++ )
		{
			_flame[i] = i < 2 ? Beam.BeamCreate( Server, BeamSpriteName, 240 ) : Beam.BeamCreate( Server, BeamSprite2, 140 );

			if ( _flame[i] is null )
				continue;

			var attach = i % 2;
			var gunPosition = Server.GetAttachment( this, attach + 1 );
			var end = (Server.Globals.Forward * FlameLength) + gunPosition;
			var trace = Util.TraceLine( gunPosition, end, DontIgnoreMonsters, this );

			_flame[i].PointEntInit( trace.EndPos, Index );

			if ( i < 2 )
				_flame[i].SetColor( 255, 130, 90 );
			else
				_flame[i].SetColor( 0, 120, 255 );

			_flame[i].SetBrightness( 190 );
			_flame[i].SetBeamFlags( (int)BeamFlags.ShadeIn );
			_flame[i].SetScrollRate( 20 );
			_flame[i].SetEndAttachment( attach + 2 );
			SoundEnt.InsertSound( WorldSound.Combat, gunPosition, 384, 0.3f );
		}

		Sound.EmitDyn( this, SoundChannel.Body, BeamAttackSounds[1], 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
		Sound.EmitDyn( this, SoundChannel.Weapon, BeamAttackSounds[2], 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
	}

	private void FlameControls( float angleX, float angleY )
	{
		if ( angleY < -180f )
			angleY += 360f;
		else if ( angleY > 180f )
			angleY -= 360f;

		if ( angleY < -45f )
			angleY = -45f;
		else if ( angleY > 45f )
			angleY = 45f;

		_flameX = Util.ApproachAngle( angleX, _flameX, 4f );
		_flameY = Util.ApproachAngle( angleY, _flameY, 8f );
		SetBoneController( 0, _flameY );
		SetBoneController( 1, _flameX );
	}

	private void FlameUpdate()
	{
		var streaks = false;

		for ( var i = 0; i < 2; i++ )
		{
			if ( _flame[i] is null )
				continue;

			var aim = Angles;

			aim.x += _flameX;
			aim.y += _flameY;

			Server.Globals.MakeVectors( aim );

			var start = Server.GetAttachment( this, i + 1 );
			var end = start + (Server.Globals.Forward * FlameLength);
			var trace = Util.TraceLine( start, end, DontIgnoreMonsters, this );

			_flame[i].SetStartPos( trace.EndPos );
			_flame[i + 2].SetStartPos( (start * 0.6f) + (trace.EndPos * 0.4f) );

			if ( trace.Fraction != 1.0 && Server.Globals.Time > _streakTime )
			{
				Util.StreakSplash( trace.EndPos, trace.PlaneNormal, 6, 20, 50, 400 );
				streaks = true;
				Util.DecalTrace( trace, Decals.SmallScorch1 + Server.RandomLong( 0, 2 ) );
			}

			FlameDamage( start, trace.EndPos, this, this, HalfLifeDll.SkillData.GargantuaDmgFire, ClassAlienMonster, DamageBurn );

			Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
			Server.WriteByte( TempEntity.ELight );
			Server.WriteShort( Index + (0x1000 * (i + 2)) );
			Server.WriteCoord( start.x );
			Server.WriteCoord( start.y );
			Server.WriteCoord( start.z );
			Server.WriteCoord( Server.RandomFloat( 32f, 48f ) );
			Server.WriteByte( 255 );
			Server.WriteByte( 255 );
			Server.WriteByte( 255 );
			Server.WriteByte( 2 );
			Server.WriteCoord( 0f );
			Server.MessageEnd();
		}

		if ( streaks )
			_streakTime = Server.Globals.Time;
	}

	private void FlameDamage( Vector3 start, Vector3 end, BaseEntity inflictor, BaseEntity attacker, float damage, int classIgnore, int damageType )
	{
		BaseEntity entity = null;

		var mid = (start + end) * 0.5f;
		var searchRadius = MathLib.Length( start - mid );
		var aim = Util.Normalize( end - start );

		while ( (entity = Util.FindEntityInSphere( entity, mid, searchRadius )) is not null )
		{
			if ( entity.DamageMode == DamageNo )
				continue;

			if ( classIgnore != ClassNone && entity.Classify() == classIgnore )
				continue;

			var spot = entity.BodyTarget( mid );
			var distance = Vector3.Dot( aim, spot - mid );

			if ( distance > searchRadius )
				distance = searchRadius;
			else if ( distance < -searchRadius )
				distance = searchRadius;

			var source = mid + (distance * aim);
			var trace = Util.TraceLine( source, spot, DontIgnoreMonsters, this );

			if ( trace.Fraction != 1.0 && trace.Ent != entity )
				continue;

			distance = MathLib.Length( source - trace.EndPos );

			float adjustedDamage;

			if ( distance > 64f )
			{
				adjustedDamage = (float)(damage - ((distance - 64f) * 0.4));

				if ( adjustedDamage <= 0f )
					continue;
			}
			else
			{
				adjustedDamage = damage;
			}

			if ( trace.Fraction != 1.0 )
			{
				Combat.ClearMultiDamage();
				entity.TraceAttack( inflictor, adjustedDamage, Util.Normalize( trace.EndPos - source ), trace, damageType );
				Combat.ApplyMultiDamage( inflictor, attacker );
			}
			else
			{
				entity.TakeDamage( inflictor, attacker, adjustedDamage, damageType );
			}
		}
	}

	private void FlameDestroy()
	{
		Sound.EmitDyn( this, SoundChannel.Weapon, BeamAttackSounds[0], 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		for ( var i = 0; i < 4; i++ )
		{
			if ( _flame[i] is null )
				continue;

			_flame[i].Remove();
			_flame[i] = null;
		}
	}

	public override void PrescheduleThink()
	{
		if ( !HasConditions( ConditionSeeEnemy ) )
		{
			_seeTime = Server.Globals.Time + 5f;
			EyeOff();
		}
		else
		{
			EyeOn( 200 );
		}

		EyeUpdate();
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/garg.mdl" );
		SetSize( new Vector3( -32f, -32f, 0f ), new Vector3( 32f, 32f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorGreen;
		Health = HalfLifeDll.SkillData.GargantuaHealth;
		FieldOfView = -0.2f;
		MonsterState = MonsterState.None;

		MonsterInit();

		_eyeGlow = Sprite.SpriteCreate( Server, EyeSpriteName, Origin, false );
		_eyeGlow.SetTransparency( RenderMode.Glow, 255, 255, 255, 0, RenderFx.NoDissipation );
		_eyeGlow.SetAttachment( this, 1 );
		EyeOff();
		_seeTime = Server.Globals.Time + 5f;
		_flameTime = Server.Globals.Time + 2f;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/garg.mdl" );
		Server.PrecacheModel( EyeSpriteName );
		Server.PrecacheModel( BeamSpriteName );
		Server.PrecacheModel( BeamSprite2 );
		Server.PrecacheModel( StompSpriteName );
		_gibModel = Server.PrecacheModel( GibModel );
		Server.PrecacheSound( StompBuzzSound );

		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( BeamAttackSounds );
		PrecacheSounds( AttackMissSounds );
		PrecacheSounds( RicSounds );
		PrecacheSounds( FootSounds );
		PrecacheSounds( IdleSounds );
		PrecacheSounds( AlertSounds );
		PrecacheSounds( PainSounds );
		PrecacheSounds( AttackSounds );
		PrecacheSounds( StompSounds );
		PrecacheSounds( BreatheSounds );
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		Util.Alert( AlertType.AiConsole, "CGargantua::TraceAttack\n" );

		if ( !IsAlive )
		{
			base.TraceAttack( attacker, damage, direction, trace, damageType );
			return;
		}

		if ( (damageType & (GargDamage | DamageBlast)) != 0 && _painSoundTime < Server.Globals.Time )
		{
			Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( PainSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
			_painSoundTime = Server.Globals.Time + Server.RandomFloat( 2.5f, 4f );
		}

		damageType &= GargDamage;

		if ( damageType == 0 )
		{
			if ( DamageTime != Server.Globals.Time || Server.RandomLong( 0, 100 ) < 20 )
			{
				Util.Ricochet( trace.EndPos, Server.RandomFloat( 0.5f, 1.5f ) );
				DamageTime = Server.Globals.Time;
			}

			damage = 0f;
		}

		base.TraceAttack( attacker, damage, direction, trace, damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Util.Alert( AlertType.AiConsole, "CGargantua::TakeDamage\n" );

		if ( IsAlive )
		{
			if ( (damageType & GargDamage) == 0 )
				damage = (float)(damage * 0.01);

			if ( (damageType & DamageBlast) != 0 )
				SetConditions( ConditionLightDamage );
		}

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	private void DeathEffect()
	{
		Server.Globals.MakeVectors( Angles );

		var deathPosition = Origin + (Server.Globals.Forward * 100f);

		Spiral.Create( Server, deathPosition, (float)((AbsMax.z - AbsMin.z) * 0.6), 125f, 1.5f );

		var position = Origin;

		position.z += 32f;

		for ( var i = 0; i < 7; i += 2 )
		{
			SpawnExplosion( position, 70f, (float)(i * 0.3), 60 + (i * 20) );
			position.z += 15f;
		}

		var smoker = Create( "env_smoker", Origin, Vector3.Zero, null );

		smoker.Health = 1f;
		smoker.Scale = 46f;
		smoker.Damage = 0f;
		smoker.NextThink = (float)(Server.Globals.Time + 2.5);
	}

	public override bool CanBecomeRagdoll() => false;

	public override void Killed( BaseEntity attacker, int gib )
	{
		EyeOff();
		_eyeGlow?.Remove();
		_eyeGlow = null;
		base.Killed( attacker, GibNever );
	}

	public override bool CheckMeleeAttack1( float dot, float distance ) => dot >= 0.7 && distance <= AttackDistance;

	public override bool CheckMeleeAttack2( float dot, float distance )
	{
		if ( Server.Globals.Time <= _flameTime )
			return false;

		return dot >= 0.8 && distance > AttackDistance && distance <= FlameLength;
	}

	public override bool CheckRangeAttack1( float dot, float distance )
	{
		if ( Server.Globals.Time <= _seeTime )
			return false;

		return dot >= 0.7 && distance > AttackDistance;
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case EventSlashLeft:
				if ( GargantuaCheckTraceHullAttack( AttackDistance + 10f, (int)HalfLifeDll.SkillData.GargantuaDmgSlash, DamageSlash ) is { } hurt )
				{
					if ( (hurt.EntityFlags & (EntityFlags.Monster | EntityFlags.Client)) != 0 )
					{
						hurt.PunchAngle = new Vector3( -30f, -30f, 30f );
						hurt.Velocity -= Server.Globals.Right * 100f;
					}

					Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, 50 + Server.RandomLong( 0, 15 ) );
				}
				else
				{
					Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackMissSounds ), 1f, Server.AttenuationNormal, 0, 50 + Server.RandomLong( 0, 15 ) );
				}

				break;

			case EventRightFoot:
			case EventLeftFoot:
				Util.ScreenShake( Origin, 4f, 3f, 1f, 750f );
				Sound.EmitDyn( this, SoundChannel.Body, RandomSound( FootSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal + Server.RandomLong( -10, 10 ) );
				break;

			case EventStomp:
				StompAttack();
				_seeTime = Server.Globals.Time + 12f;
				break;

			case EventBreathe:
				Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( BreatheSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal + Server.RandomLong( -10, 10 ) );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	private BaseEntity GargantuaCheckTraceHullAttack( float distance, int damage, int damageType )
	{
		Server.Globals.MakeVectors( Angles );

		var start = Origin;

		start.z += 64f;

		var end = start + (Server.Globals.Forward * distance) - (Server.Globals.Up * distance * 0.3f);
		var trace = Util.TraceHull( start, end, DontIgnoreMonsters, HeadHull, this );

		if ( trace.Ent is not BaseEntity entity )
			return null;

		if ( damage > 0 )
			entity.TakeDamage( this, this, damage, damageType );

		return entity;
	}

	public override Schedule GetScheduleOfType( int type )
	{
		if ( FlameIsOn )
			FlameDestroy();

		return type switch
		{
			ScheduleType.MeleeAttack2 => GargantuaAi.GargFlame,
			ScheduleType.MeleeAttack1 => GargantuaAi.GargSwipe,
			_ => base.GetScheduleOfType( type )
		};
	}

	public override void StartTask( Task task )
	{
		switch ( task.Id )
		{
			case GargTask.FlameSweep:
				FlameCreate();
				WaitFinished = Server.Globals.Time + task.Data;
				_flameTime = Server.Globals.Time + 6f;
				_flameX = 0f;
				_flameY = 0f;
				break;

			case GargTask.SoundAttack:
				if ( Server.RandomLong( 0, 100 ) < 30 )
					Sound.EmitDyn( this, SoundChannel.Voice, RandomSound( AttackSounds ), 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

				TaskComplete();
				break;

			case TaskId.Die:
				WaitFinished = (float)(Server.Globals.Time + 1.6);
				DeathEffect();
				base.StartTask( task );
				break;

			default:
				base.StartTask( task );
				break;
		}
	}

	public override void RunTask( Task task )
	{
		switch ( task.Id )
		{
			case TaskId.Die:
				if ( Server.Globals.Time > WaitFinished )
					Explode();
				else
					base.RunTask( task );

				break;

			case GargTask.FlameSweep:
				if ( Server.Globals.Time > WaitFinished )
				{
					FlameDestroy();
					TaskComplete();
					FlameControls( 0f, 0f );
					SetBoneController( 0, 0f );
					SetBoneController( 1, 0f );
					break;
				}

				var cancel = false;
				var angles = Vector3.Zero;

				FlameUpdate();

				if ( EnemyEntity is { } enemy )
				{
					var origin = Origin;

					origin.z += 64f;

					var direction = enemy.BodyTarget( origin ) - origin;

					angles = MathLib.VectorAngles( direction );
					angles.x = -angles.x;
					angles.y -= Angles.y;

					if ( MathLib.Length( direction ) > 400f )
						cancel = true;
				}

				if ( MathF.Abs( angles.y ) > 60f )
					cancel = true;

				if ( cancel )
				{
					WaitFinished -= 0.5f;
					_flameTime -= 0.5f;
				}

				FlameControls( angles.x, angles.y );
				break;

			default:
				base.RunTask( task );
				break;
		}
	}

	private void Explode()
	{
		RenderFx = RenderFx.Explode;
		RenderColor = new Vector3( 255f, 0f, 0f );
		StopAnimation();
		NextThink = (float)(Server.Globals.Time + 0.15);
		SetThink( SubRemove );

		var parts = Server.ModelFrames( _gibModel );

		for ( var i = 0; i < 10; i++ )
		{
			var gib = Server.CreateEntity<Gib>();

			gib.Spawn( GibModel );

			var bodyPart = 0;

			if ( parts > 1 )
				bodyPart = Server.RandomLong( 0, Body - 1 );

			gib.Body = bodyPart;
			gib.GibBloodColor = BloodColorYellow;
			gib.Material = Materials.None;
			gib.Origin = Origin;
			gib.Velocity = Util.RandomBloodVector() * Server.RandomFloat( 300f, 500f );
			gib.NextThink = (float)(Server.Globals.Time + 1.25);
			gib.SetThink( gib.SubFadeOut );
		}

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.BreakModel );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( 200f );
		Server.WriteCoord( 200f );
		Server.WriteCoord( 128f );
		Server.WriteCoord( 0f );
		Server.WriteCoord( 0f );
		Server.WriteCoord( 0f );
		Server.WriteByte( 200 );
		Server.WriteShort( _gibModel );
		Server.WriteByte( 50 );
		Server.WriteByte( 20 );
		Server.WriteByte( Breakable.BreakFlesh );
		Server.MessageEnd();
	}

	private void SpawnExplosion( Vector3 center, float randomRange, float time, int magnitude )
	{
		center.x += Server.RandomFloat( -randomRange, randomRange );
		center.y += Server.RandomFloat( -randomRange, randomRange );

		var explosion = Create( "env_explosion", center, Vector3.Zero, null );

		explosion.KeyValue( "iMagnitude", $"{magnitude,3}" );
		explosion.SpawnFlags |= EnvExplosion.SpawnFlagNoDamage;

		explosion.Spawn();
		explosion.SetThink( explosion.CallUseToggle );
		explosion.NextThink = Server.Globals.Time + time;
	}
}
