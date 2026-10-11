using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

public class Gib : BaseEntity
{
	private const int GermanGibCount = 4;
	private const int HumanGibCount = 6;
	private const int AlienGibCount = 4;

	public int GibBloodColor;
	public int BloodDecals;
	public Materials Material;
	public float LifeTime;

	public override ObjectCaps ObjectCaps => (base.ObjectCaps & ~ObjectCaps.AcrossTransition) | ObjectCaps.DontSave;

	public void LimitVelocity()
	{
		if ( MathLib.Length( Velocity ) > 1500f )
			Velocity = Util.Normalize( Velocity ) * 1500f;
	}

	public static void SpawnStickyGibs( BaseEntity victim, Vector3 origin, int count )
	{
		if ( HalfLifeDll.Language == HalfLifeDll.LanguageGerman )
			return;

		var server = Server.Current;

		for ( var i = 0; i < count; i++ )
		{
			var gib = server.CreateEntity<Gib>();

			gib.Spawn( "models/stickygib.mdl" );
			gib.Body = Server.RandomLong( 0, 2 );

			if ( victim is not null )
			{
				var x = origin.x + Server.RandomFloat( -3f, 3f );
				var y = origin.y + Server.RandomFloat( -3f, 3f );
				var z = origin.z + Server.RandomFloat( -3f, 3f );

				gib.Origin = new Vector3( x, y, z );

				var velocity = AttackDir * -1f;

				velocity.x += Server.RandomFloat( -0.15f, 0.15f );
				velocity.y += Server.RandomFloat( -0.15f, 0.15f );
				velocity.z += Server.RandomFloat( -0.15f, 0.15f );

				gib.Velocity = velocity * 900f;

				var pitch = Server.RandomFloat( 250f, 400f );
				var yaw = Server.RandomFloat( 250f, 400f );

				gib.AVelocity = new Vector3( pitch, yaw, gib.AVelocity.z );
				gib.GibBloodColor = victim.BloodColor;

				gib.ScaleVelocityByHealth( victim );

				gib.MoveType = MoveType.Toss;
				gib.Solid = Solid.BBox;
				gib.SetSize( Vector3.Zero, Vector3.Zero );
				gib.SetTouch( gib.StickyGibTouch );
				gib.SetThink( null );
			}

			gib.LimitVelocity();
		}
	}

	public static void SpawnHeadGib( BaseEntity victim )
	{
		var server = Server.Current;
		var gib = server.CreateEntity<Gib>();

		gib.Spawn( HalfLifeDll.Language == HalfLifeDll.LanguageGerman ? "models/germangibs.mdl" : "models/hgibs.mdl" );
		gib.Body = 0;

		if ( victim is not null )
		{
			gib.Origin = victim.Origin + victim.ViewOffset;

			var player = server.FindClientInPvs( gib );

			if ( Server.RandomLong( 0, 100 ) <= 5 && player is not null )
			{
				var velocity = Util.Normalize( player.Origin + player.ViewOffset - gib.Origin ) * 300f;

				velocity.z += 100f;
				gib.Velocity = velocity;
			}
			else
			{
				var x = Server.RandomFloat( -100f, 100f );
				var y = Server.RandomFloat( -100f, 100f );
				var z = Server.RandomFloat( 200f, 300f );

				gib.Velocity = new Vector3( x, y, z );
			}

			var pitch = Server.RandomFloat( 100f, 200f );
			var yaw = Server.RandomFloat( 100f, 300f );

			gib.AVelocity = new Vector3( pitch, yaw, gib.AVelocity.z );
			gib.GibBloodColor = victim.BloodColor;

			gib.ScaleVelocityByHealth( victim );
		}

		gib.LimitVelocity();
	}

	public static void SpawnRandomGibs( BaseEntity victim, int count, bool human )
	{
		var server = Server.Current;

		for ( var i = 0; i < count; i++ )
		{
			var gib = server.CreateEntity<Gib>();

			if ( HalfLifeDll.Language == HalfLifeDll.LanguageGerman )
			{
				gib.Spawn( "models/germangibs.mdl" );
				gib.Body = Server.RandomLong( 0, GermanGibCount - 1 );
			}
			else if ( human )
			{
				gib.Spawn( "models/hgibs.mdl" );
				gib.Body = Server.RandomLong( 1, HumanGibCount - 1 );
			}
			else
			{
				gib.Spawn( "models/agibs.mdl" );
				gib.Body = Server.RandomLong( 0, AlienGibCount - 1 );
			}

			if ( victim is not null )
			{
				var x = victim.AbsMin.x + (victim.Size.x * Server.RandomFloat( 0f, 1f ));
				var y = victim.AbsMin.y + (victim.Size.y * Server.RandomFloat( 0f, 1f ));
				var z = victim.AbsMin.z + (victim.Size.z * Server.RandomFloat( 0f, 1f )) + 1f;

				gib.Origin = new Vector3( x, y, z );

				var velocity = AttackDir * -1f;

				velocity.x += Server.RandomFloat( -0.25f, 0.25f );
				velocity.y += Server.RandomFloat( -0.25f, 0.25f );
				velocity.z += Server.RandomFloat( -0.25f, 0.25f );

				gib.Velocity = velocity * Server.RandomFloat( 300f, 400f );

				var pitch = Server.RandomFloat( 100f, 200f );
				var yaw = Server.RandomFloat( 100f, 300f );

				gib.AVelocity = new Vector3( pitch, yaw, gib.AVelocity.z );
				gib.GibBloodColor = victim.BloodColor;

				gib.ScaleVelocityByHealth( victim );

				gib.Solid = Solid.BBox;
				gib.SetSize( Vector3.Zero, Vector3.Zero );
			}

			gib.LimitVelocity();
		}
	}

	public void Spawn( string model )
	{
		MoveType = MoveType.Bounce;
		Friction = 0.55f;
		RenderAmt = 255f;
		RenderMode = RenderMode.Normal;
		RenderFx = RenderFx.None;
		Solid = Solid.SlideBox;
		ClassName = "gib";

		SetModel( model );
		SetSize( Vector3.Zero, Vector3.Zero );

		NextThink = Server.Globals.Time + 4f;
		LifeTime = 25f;

		SetThink( WaitTillLand );
		SetTouch( BounceGibTouch );

		Material = Materials.None;
		BloodDecals = 5;
	}

	private void ScaleVelocityByHealth( BaseEntity victim )
	{
		if ( victim.Health > -50f )
			Velocity *= 0.7f;
		else if ( victim.Health > -200f )
			Velocity *= 2f;
		else
			Velocity *= 4f;
	}

	private void WaitTillLand()
	{
		if ( !IsInWorld )
		{
			Remove();
			return;
		}

		if ( MathLib.VectorCompare( Velocity, Vector3.Zero ) )
		{
			SetThink( SubStartFadeOut );
			NextThink = Server.Globals.Time + LifeTime;

			if ( GibBloodColor != DontBleed )
				SoundEnt.InsertSound( WorldSound.Meat, Origin, 384, 25f );
		}
		else
		{
			NextThink = (float)(Server.Globals.Time + 0.5);
		}
	}

	private void BounceGibTouch( BaseEntity other )
	{
		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			Velocity *= 0.9f;
			Angles = new Vector3( 0f, Angles.y, 0f );
			AVelocity = new Vector3( 0f, AVelocity.y, 0f );
			return;
		}

		if ( HalfLifeDll.Language != HalfLifeDll.LanguageGerman && BloodDecals > 0 && GibBloodColor != DontBleed )
		{
			var spot = Origin + new Vector3( 0f, 0f, 8f );
			var trace = Util.TraceLine( spot, spot + new Vector3( 0f, 0f, -24f ), IgnoreMonsters, this );

			Util.BloodDecalTrace( trace, GibBloodColor );
			BloodDecals--;
		}

		if ( Material != Materials.None && Server.RandomLong( 0, 2 ) == 0 )
		{
			var zVelocity = MathF.Abs( Velocity.z );
			var volume = (float)(0.8 * Math.Min( 1.0, zVelocity / 450.0 ));

			Breakable.MaterialSoundRandom( this, Material, volume );
		}
	}

	private void StickyGibTouch( BaseEntity other )
	{
		SetThink( SubRemove );
		NextThink = Server.Globals.Time + 10f;

		if ( other.ClassName != "worldspawn" )
		{
			NextThink = Server.Globals.Time;
			return;
		}

		var trace = Util.TraceLine( Origin, Origin + (Velocity * 32f), IgnoreMonsters, this );

		Util.BloodDecalTrace( trace, GibBloodColor );

		Velocity = trace.PlaneNormal * -1f;
		Angles = MathLib.VectorAngles( Velocity );
		Velocity = Vector3.Zero;
		AVelocity = Vector3.Zero;
		MoveType = MoveType.None;
	}
}

public static partial class Combat
{
	public static void RadiusDamage( Vector3 source, BaseEntity inflictor, BaseEntity attacker, float damage, float radius, int classIgnore, int damageType, Vector3? forceSource = null )
	{
		var falloff = radius != 0f ? damage / radius : 1f;
		var inWater = Util.PointContents( source ) == (int)Contents.Water;

		source.z += 1f;

		var forceOrigin = forceSource is { } origin ? origin + new Vector3( 0f, 0f, 1f ) : source;

		attacker ??= inflictor;

		BaseEntity entity = null;

		while ( (entity = Util.FindEntityInSphere( entity, source, radius )) is not null )
		{
			if ( entity.DamageMode == BaseEntity.DamageNo )
				continue;

			if ( classIgnore != BaseEntity.ClassNone && entity.Classify() == classIgnore )
				continue;

			if ( inWater && entity.WaterLevel == 0 )
				continue;

			if ( !inWater && entity.WaterLevel == 3 )
				continue;

			var spot = entity.BodyTarget( source );
			var trace = Util.TraceLine( source, spot, BaseEntity.DontIgnoreMonsters, inflictor );

			if ( trace.Fraction != 1f && trace.Ent != entity )
				continue;

			if ( trace.StartSolid )
			{
				trace.EndPos = source;
				trace.Fraction = 0f;
			}

			var adjustedDamage = damage - (MathLib.Length( source - trace.EndPos ) * falloff);

			if ( adjustedDamage < 0f )
				adjustedDamage = 0f;

			var force = ScenePhysicsDamage.ExplosiveForce( damage, spot - forceOrigin );

			if ( trace.Fraction != 1f )
			{
				Combat.ClearMultiDamage();
				Combat.DamageForce = force;
				entity.TraceAttack( inflictor, adjustedDamage, Util.Normalize( trace.EndPos - source ), trace, damageType );
				Combat.ApplyMultiDamage( inflictor, attacker );
			}
			else
			{
				Combat.DamageForce = force;
				entity.TakeDamage( inflictor, attacker, adjustedDamage, damageType );
				Combat.DamageForce = Vector3.Zero;
			}
		}

		ScenePhysicsDamage.Explosion( source, inflictor, damage, radius, falloff );
	}
}

partial class BaseMonster
{
	private const float RagdollRemoveDelay = 2f;

	private Vector3 _damageForce;

	protected int ForceBone { get; set; }

	public virtual bool HasHumanGibs()
	{
		return Classify() is ClassHumanMilitary or ClassPlayerAlly or ClassHumanPassive or ClassPlayer;
	}

	public virtual bool HasAlienGibs()
	{
		return Classify() is ClassAlienMilitary or ClassAlienMonster or ClassAlienPassive or ClassInsect or ClassAlienPredator or ClassAlienPrey;
	}

	public virtual void FadeMonster()
	{
		StopAnimation();

		Velocity = Vector3.Zero;
		MoveType = MoveType.None;
		AVelocity = Vector3.Zero;
		AnimTime = Server.Globals.Time;
		Effects |= Effects.NoInterp;

		SubStartFadeOut();
	}

	public virtual void GibMonster()
	{
		var gibbed = false;

		Sound.Emit( this, SoundChannel.Weapon, "common/bodysplat.wav", 1f, Server.AttenuationNormal );

		if ( HasHumanGibs() )
		{
			if ( Cvar.GetFloat( "violence_hgibs" ) != 0f )
			{
				Gib.SpawnHeadGib( this );
				Gib.SpawnRandomGibs( this, 4, true );
			}

			gibbed = true;
		}
		else if ( HasAlienGibs() )
		{
			if ( Cvar.GetFloat( "violence_agibs" ) != 0f )
				Gib.SpawnRandomGibs( this, 4, false );

			gibbed = true;
		}

		if ( IsPlayer )
			return;

		if ( gibbed )
		{
			SetThink( SubRemove );
			NextThink = Server.Globals.Time;
		}
		else
		{
			FadeMonster();
		}
	}

	public virtual Activity GetDeathActivity()
	{
		if ( DeadFlag != DeadNo )
			return IdealActivity;

		var source = Center;
		var triedDirection = false;
		var deathActivity = Activity.DieSimple;

		Server.Globals.MakeVectors( Angles );

		var dot = Vector3.Dot( Server.Globals.Forward, AttackDir * -1f );

		switch ( LastHitGroup )
		{
			case HitGroupHead:
				deathActivity = Activity.DieHeadshot;
				break;

			case HitGroupStomach:
				deathActivity = Activity.DieGutshot;
				break;

			default:
				triedDirection = true;

				if ( dot > 0.3 )
					deathActivity = Activity.DieForward;
				else if ( dot <= -0.3 )
					deathActivity = Activity.DieBackward;

				break;
		}

		if ( LookupActivity( (int)deathActivity ) == Animation.ActivityNotAvailable )
		{
			if ( triedDirection )
				deathActivity = Activity.DieSimple;
			else if ( dot > 0.3 )
				deathActivity = Activity.DieForward;
			else if ( dot <= -0.3 )
				deathActivity = Activity.DieBackward;
		}

		if ( LookupActivity( (int)deathActivity ) == Animation.ActivityNotAvailable )
			deathActivity = Activity.DieSimple;

		if ( deathActivity == Activity.DieForward )
		{
			var trace = Util.TraceHull( source, source + (Server.Globals.Forward * 64f), DontIgnoreMonsters, HeadHull, this );

			if ( trace.Fraction != 1f )
				deathActivity = Activity.DieSimple;
		}

		if ( deathActivity == Activity.DieBackward )
		{
			var trace = Util.TraceHull( source, source - (Server.Globals.Forward * 64f), DontIgnoreMonsters, HeadHull, this );

			if ( trace.Fraction != 1f )
				deathActivity = Activity.DieSimple;
		}

		return deathActivity;
	}

	public Activity GetSmallFlinchActivity()
	{
		Server.Globals.MakeVectors( Angles );

		var flinchActivity = LastHitGroup switch
		{
			HitGroupHead => Activity.FlinchHead,
			HitGroupStomach => Activity.FlinchStomach,
			HitGroupLeftArm => Activity.FlinchLeftArm,
			HitGroupRightArm => Activity.FlinchRightArm,
			HitGroupLeftLeg => Activity.FlinchLeftLeg,
			HitGroupRightLeg => Activity.FlinchRightLeg,
			_ => Activity.SmallFlinch
		};

		if ( LookupActivity( (int)flinchActivity ) == Animation.ActivityNotAvailable )
			flinchActivity = Activity.SmallFlinch;

		return flinchActivity;
	}

	public virtual void BecomeDead()
	{
		DamageMode = DamageYes;
		Health = MaxHealth / 2f;
		MaxHealth = 5f;
		MoveType = MoveType.Toss;
	}

	public bool ShouldGibMonster( int gib )
	{
		return (gib == GibNormal && Health < GibHealthValue) || gib == GibAlways;
	}

	public void CallGibMonster()
	{
		var fade = false;

		if ( HasHumanGibs() )
		{
			if ( Cvar.GetFloat( "violence_hgibs" ) == 0f )
				fade = true;
		}
		else if ( HasAlienGibs() )
		{
			if ( Cvar.GetFloat( "violence_agibs" ) == 0f )
				fade = true;
		}

		DamageMode = DamageNo;
		Solid = Solid.Not;

		if ( fade )
		{
			FadeMonster();
		}
		else
		{
			Effects = Effects.NoDraw;
			GibMonster();
		}

		DeadFlag = DeadDead;
		CheckAiTrigger();

		if ( Health < -99f )
			Health = 0f;

		if ( ShouldFadeOnDeath() && !fade )
			Remove();
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		if ( HasMemory( MemoryKilled ) )
		{
			if ( ShouldGibMonster( gib ) )
				CallGibMonster();

			return;
		}

		Remember( MemoryKilled );

		Sound.Emit( this, SoundChannel.Weapon, "common/null.wav", 1f, Server.AttenuationNormal );

		IdealMonsterState = MonsterState.Dead;
		SetConditions( ConditionLightDamage );

		(Owner as BaseEntity)?.DeathNotice( this );

		if ( ShouldGibMonster( gib ) )
		{
			CallGibMonster();
			return;
		}

		if ( ShouldBecomeRagdoll( gib ) )
		{
			BecomeRagdoll();
			return;
		}

		if ( (EntityFlags & EntityFlags.Monster) == EntityFlags.Monster )
		{
			SetTouch( null );
			BecomeDead();
		}

		if ( Health < -99f )
			Health = 0f;

		IdealMonsterState = MonsterState.Dead;
	}

	public virtual bool CanBecomeRagdoll() => false;

	private bool RagdollAllowed() => GameCvars.Ragdolls != 0f && CanBecomeRagdoll() && ClientRagdoll.Supports( Server.GetRenderModel( ModelIndex ) );

	protected bool ShouldBecomeRagdoll( int gib ) => RagdollAllowed() && !ShouldGibMonster( gib );

	private void BecomeRagdoll()
	{
		Server.MessageBegin( MessageDest.All, UserMessages.Ragdoll );
		Server.WriteShort( Index );
		Server.WriteLong( BitConverter.SingleToInt32Bits( _damageForce.x ) );
		Server.WriteLong( BitConverter.SingleToInt32Bits( _damageForce.y ) );
		Server.WriteLong( BitConverter.SingleToInt32Bits( _damageForce.z ) );
		Server.WriteByte( ForceBone );
		Server.MessageEnd();

		DeathSound();

		Solid = Solid.Not;
		MoveType = MoveType.None;
		DamageMode = DamageNo;
		Effects |= Effects.NoDraw;
		DeadFlag = DeadDead;

		SetTouch( null );
		CheckAiTrigger();

		SetThink( SubRemove );
		NextThink = Server.Globals.Time + RagdollRemoveDelay;
	}

	private Vector3 CalcDamageForceVector( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType, Vector3 damageForce )
	{
		if ( damageForce != Vector3.Zero || (damageType & DamageNoPhysicsForce) != 0 )
		{
			if ( (damageType & DamageBlast) == 0 )
				return damageForce;

			var scale = Server.RandomFloat( 0.85f, 1.15f );

			return new Vector3( damageForce.x * scale, damageForce.y * scale, damageForce.z * (damageForce.z > 0f ? 1.15f : scale) );
		}

		var source = inflictor ?? attacker;

		if ( source is null )
			return Vector3.Zero;

		var forceScale = damage * 75f * 4f;
		Vector3 force;

		if ( (damageType & DamageBlast) != 0 )
		{
			force = Util.Normalize( Origin + new Vector3( 0f, 0f, Size.z ) - source.Origin ) * 1.375f;
		}
		else if ( source == this )
		{
			force = new Vector3( Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ), 0f );
			forceScale = Server.RandomFloat( 1000f, 2000f );
		}
		else
		{
			force = Util.Normalize( Origin - source.Origin );
		}

		return force * forceScale;
	}

	public override bool TakeHealth( float health, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		DamageTypeBits &= ~(damageType & ~DamageTimeBased);

		return base.TakeHealth( health, damageType );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( DamageMode == DamageNo )
			return false;

		if ( !IsAlive )
			return DeadTakeDamage( inflictor, attacker, damage, damageType );

		if ( DeadFlag == DeadNo )
			PainSound();

		var take = damage;

		DamageTypeBits |= damageType;

		var direction = Vector3.Zero;

		if ( !Util.IsNullEnt( inflictor ) )
		{
			direction = Util.Normalize( inflictor.Center - new Vector3( 0f, 0f, 10f ) - Center );
			direction = AttackDir = Util.Normalize( direction );
		}

		if ( IsPlayer )
		{
			if ( inflictor is not null )
				DamageInflictor = inflictor;

			DamageTake += take;

			if ( (EntityFlags & EntityFlags.GodMode) == EntityFlags.GodMode )
				return false;
		}

		if ( !Util.IsNullEnt( inflictor ) && MoveType == MoveType.Walk && (attacker is null || attacker.Solid != Solid.Trigger) )
			Velocity += direction * -DamageForce( damage );

		Health -= take;

		if ( MonsterState == MonsterState.Script )
		{
			if ( Health > 0f || !RagdollAllowed() )
			{
				SetConditions( ConditionLightDamage );
				return false;
			}

			Cine?.CancelScript();
		}

		if ( Health <= 0f )
		{
			HalfLifeDll.LastInflictor = inflictor;

			_damageForce = CalcDamageForceVector( inflictor, attacker, damage, damageType, Combat.DamageForce );

			if ( (damageType & DamageAlwaysGib) != 0 )
				Killed( attacker, GibAlways );
			else if ( (damageType & DamageNeverGib) != 0 )
				Killed( attacker, GibNever );
			else
				Killed( attacker, GibNormal );

			HalfLifeDll.LastInflictor = null;

			return false;
		}

		if ( (EntityFlags & EntityFlags.Monster) == EntityFlags.Monster && !Util.IsNullEnt( attacker ) && (attacker.EntityFlags & (EntityFlags.Monster | EntityFlags.Client)) != 0 )
		{
			if ( inflictor is not null )
			{
				if ( EnemyEntity is null || inflictor == EnemyEntity || !HasConditions( ConditionSeeEnemy ) )
					EnemyLastKnownPosition = inflictor.Origin;
			}
			else
			{
				EnemyLastKnownPosition = Origin + (AttackDir * 64f);
			}

			MakeIdealYaw( EnemyLastKnownPosition );

			if ( damage > 0f )
				SetConditions( ConditionLightDamage );

			if ( damage >= 20f )
				SetConditions( ConditionHeavyDamage );
		}

		return true;
	}

	public bool DeadTakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( !Util.IsNullEnt( inflictor ) )
			AttackDir = Util.Normalize( Util.Normalize( inflictor.Center - new Vector3( 0f, 0f, 10f ) - Center ) );

		if ( (damageType & DamageGibCorpse) != 0 )
		{
			if ( Health <= damage )
			{
				Health = -50f;
				Killed( attacker, GibAlways );
				return false;
			}

			Health = (float)(Health - (damage * 0.1));
		}

		return true;
	}

	public float DamageForce( float damage )
	{
		var force = (float)(damage * (32 * 32 * 72.0 / (Size.x * Size.y * Size.z)) * 5);

		if ( force > 1000.0 )
			force = 1000f;

		return force;
	}

	public void RadiusDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int classIgnore, int damageType )
	{
		Combat.RadiusDamage( Origin, inflictor, attacker, damage, (float)(damage * 2.5), classIgnore, damageType );
	}

	public void RadiusDamage( Vector3 source, BaseEntity inflictor, BaseEntity attacker, float damage, int classIgnore, int damageType )
	{
		Combat.RadiusDamage( source, inflictor, attacker, damage, (float)(damage * 2.5), classIgnore, damageType );
	}

	public BaseEntity CheckTraceHullAttack( float distance, int damage, int damageType )
	{
		if ( IsPlayer )
			Server.Globals.MakeVectors( Angles );
		else
			Util.MakeAimVectors( Angles, Server.Globals );

		var start = Origin;

		start.z += Size.z * 0.5f;

		var end = start + (Server.Globals.Forward * distance);
		var trace = Util.TraceHull( start, end, DontIgnoreMonsters, HeadHull, this );

		if ( trace.Ent is not BaseEntity entity )
			return null;

		if ( damage > 0 )
		{
			Combat.DamageForce = ScenePhysicsDamage.MeleeForce( damage, entity.Center - Center );

			entity.TakeDamage( this, this, damage, damageType );

			Combat.DamageForce = Vector3.Zero;
		}

		return entity;
	}

	public virtual bool IsInViewCone( BaseEntity entity ) => IsInViewCone( entity.Origin );

	public virtual bool IsInViewCone( Vector3 origin )
	{
		Server.Globals.MakeVectors( Angles );

		var lineOfSight = Util.Normalize2D( origin - Origin );
		var dot = (lineOfSight.x * Server.Globals.Forward.x) + (lineOfSight.y * Server.Globals.Forward.y);

		return dot > FieldOfView;
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( DamageMode == DamageNo )
			return;

		LastHitGroup = trace.HitGroup;
		ForceBone = trace.HitBone;

		switch ( trace.HitGroup )
		{
			case HitGroupHead:
				damage *= HalfLifeDll.SkillData.MonHead;
				break;
			case HitGroupChest:
				damage *= HalfLifeDll.SkillData.MonChest;
				break;
			case HitGroupStomach:
				damage *= HalfLifeDll.SkillData.MonStomach;
				break;
			case HitGroupLeftArm:
			case HitGroupRightArm:
				damage *= HalfLifeDll.SkillData.MonArm;
				break;
			case HitGroupLeftLeg:
			case HitGroupRightLeg:
				damage *= HalfLifeDll.SkillData.MonLeg;
				break;
		}

		Combat.SpawnBlood( trace.EndPos, BloodColor, damage );
		TraceBleed( damage, direction, trace, damageType );
		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}

	public void MakeDamageBloodDecal( int count, float noise, in Trace trace, Vector3 direction )
	{
		if ( !IsAlive )
		{
			if ( MaxHealth <= 0f )
				return;

			MaxHealth--;
		}

		for ( var i = 0; i < count; i++ )
		{
			var traceDirection = direction;

			traceDirection.x += Server.RandomFloat( -noise, noise );
			traceDirection.y += Server.RandomFloat( -noise, noise );
			traceDirection.z += Server.RandomFloat( -noise, noise );

			var bloodTrace = Util.TraceLine( trace.EndPos, trace.EndPos + (traceDirection * 172f), IgnoreMonsters, this );

			if ( bloodTrace.Fraction != 1f )
				Util.BloodDecalTrace( bloodTrace, BloodColor );
		}
	}
}
