using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_barnacle" )]
public class Barnacle : BaseMonster
{
	private const float BodyHeight = 44f;
	private const float PullSpeed = 8f;
	private const float CheckSpacing = 8f;

	private const int AnimEventPukeGib = 2;

	private static readonly string[] ChewSounds = ["barnacle/bcl_chew1.wav", "barnacle/bcl_chew2.wav", "barnacle/bcl_chew3.wav"];

	[Save( FieldType.Float )]
	private float _altitude;
	[Save( FieldType.Time )]
	private float _killVictimTime;
	[Save( FieldType.Integer )]
	private int _gibs;
	[Save( FieldType.Boolean )]
	private bool _tongueExtended;
	[Save( FieldType.Boolean )]
	private bool _liftingPrey;
	[Save( FieldType.Float )]
	private float _tongueAdjust;

	public override int Classify() => ClassAlienMonster;

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		switch ( monsterEvent.Event )
		{
			case AnimEventPukeGib:
				Gib.SpawnRandomGibs( this, 1, true );
				break;

			default:
				base.HandleAnimEvent( monsterEvent );
				break;
		}
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/barnacle.mdl" );
		SetSize( new Vector3( -16f, -16f, -32f ), new Vector3( 16f, 16f, 0f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.None;
		DamageMode = DamageAim;
		MonsterBloodColor = BloodColorRed;
		Effects = Effects.InvLight;
		Health = 25f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;
		_killVictimTime = 0f;
		_gibs = 0;
		_liftingPrey = false;
		_tongueAdjust = -100f;

		InitBoneControllers();

		SetActivity( Activity.Idle );

		SetThink( BarnacleThink );
		NextThink = (float)(Server.Globals.Time + 0.5);

		SetOrigin( Origin );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( (damageType & DamageClub) != 0 )
			damage = Health;

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public void BarnacleThink()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( EnemyEntity is { } enemy )
		{
			if ( !enemy.IsAlive )
			{
				_liftingPrey = false;
				EnemyEntity = null;
				return;
			}

			if ( _liftingPrey )
			{
				if ( enemy.DeadFlag != DeadNo )
				{
					EnemyEntity = null;
					_liftingPrey = false;
					return;
				}

				var newEnemyOrigin = enemy.Origin;

				newEnemyOrigin.x = Origin.x;
				newEnemyOrigin.y = Origin.y;

				newEnemyOrigin.x = (float)(newEnemyOrigin.x - (6 * Math.Cos( enemy.Angles.y * Math.PI / 180.0 )));
				newEnemyOrigin.y = (float)(newEnemyOrigin.y - (6 * Math.Sin( enemy.Angles.y * Math.PI / 180.0 )));

				_altitude -= PullSpeed;
				newEnemyOrigin.z += PullSpeed;

				if ( Math.Abs( Origin.z - (newEnemyOrigin.z + enemy.ViewOffset.z - 8f) ) < BodyHeight )
				{
					_liftingPrey = false;

					Sound.Emit( this, SoundChannel.Weapon, "barnacle/bcl_bite3.wav", 1f, Server.AttenuationNormal );

					_killVictimTime = Server.Globals.Time + 10f;

					if ( enemy is BaseMonster victim )
					{
						victim.BarnacleVictimBitten( this );
						SetActivity( Activity.Eat );
					}
				}

				Server.SetOrigin( enemy, newEnemyOrigin );
			}
			else
			{
				var victim = enemy as BaseMonster;

				if ( _killVictimTime != -1f && Server.Globals.Time > _killVictimTime )
				{
					if ( victim is not null )
					{
						victim.TakeDamage( this, this, victim.Health, DamageSlash | DamageAlwaysGib );
						_gibs = 3;
					}

					return;
				}

				if ( victim is not null && Server.RandomLong( 0, 49 ) == 0 )
				{
					ChewSound();
					victim.BarnacleVictimBitten( this );
				}
			}
		}
		else
		{
			if ( Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
				NextThink = Server.Globals.Time + Server.RandomFloat( 1f, 1.5f );

			if ( SequenceFinished )
			{
				SetActivity( Activity.Idle );
				_tongueAdjust = -100f;
			}

			if ( _gibs != 0 && Server.RandomLong( 0, 99 ) == 1 )
			{
				Gib.SpawnRandomGibs( this, 1, true );
				_gibs--;

				ChewSound();
			}

			var touched = TongueTouchEnt( out var length );

			if ( touched is not null && _tongueExtended )
			{
				if ( touched.BecomeProne() )
				{
					Sound.Emit( this, SoundChannel.Weapon, "barnacle/bcl_alert2.wav", 1f, Server.AttenuationNormal );

					SetSequenceByName( "attack1" );
					_tongueAdjust = -20f;

					EnemyEntity = touched;

					touched.MoveType = MoveType.Fly;
					touched.Velocity = Vector3.Zero;
					touched.BaseVelocity = Vector3.Zero;
					touched.Origin = touched.Origin with { x = Origin.x, y = Origin.y };

					_liftingPrey = true;
					_killVictimTime = -1f;

					_altitude = Origin.z - touched.EyePosition.z;
				}
			}
			else if ( _altitude < length )
			{
				_altitude += PullSpeed;
				_tongueExtended = false;
			}
			else
			{
				_altitude = length;
				_tongueExtended = true;
			}
		}

		SetBoneController( 0, -(_altitude + _tongueAdjust) );
		StudioFrameAdvance( 0.1f );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		Solid = Solid.Not;
		DamageMode = DamageNo;

		if ( EnemyEntity is BaseMonster victim )
			victim.BarnacleVictimReleased();

		switch ( Server.RandomLong( 0, 1 ) )
		{
			case 0:
				Sound.Emit( this, SoundChannel.Weapon, "barnacle/bcl_die1.wav", 1f, Server.AttenuationNormal );
				break;
			case 1:
				Sound.Emit( this, SoundChannel.Weapon, "barnacle/bcl_die3.wav", 1f, Server.AttenuationNormal );
				break;
		}

		SetActivity( Activity.DieSimple );
		SetBoneController( 0, 0f );

		StudioFrameAdvance( 0.1f );

		NextThink = (float)(Server.Globals.Time + 0.1);
		SetThink( WaitTillDead );
	}

	public void WaitTillDead()
	{
		NextThink = (float)(Server.Globals.Time + 0.1);

		var interval = StudioFrameAdvance( 0.1f );

		DispatchAnimEvents( interval );

		if ( SequenceFinished )
		{
			StopAnimation();
			SetThink( null );
		}
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/barnacle.mdl" );

		Server.PrecacheSound( "barnacle/bcl_alert2.wav" );
		Server.PrecacheSound( "barnacle/bcl_bite3.wav" );
		PrecacheSounds( ChewSounds );
		Server.PrecacheSound( "barnacle/bcl_die1.wav" );
		Server.PrecacheSound( "barnacle/bcl_die3.wav" );
	}

	private void ChewSound()
	{
		Sound.Emit( this, SoundChannel.Weapon, ChewSounds[Server.RandomLong( 0, 2 )], 1f, Server.AttenuationNormal );
	}

	public BaseEntity TongueTouchEnt( out float length )
	{
		var trace = Util.TraceLine( Origin, Origin - new Vector3( 0f, 0f, 2048f ), IgnoreMonsters, this );

		length = Math.Abs( Origin.z - trace.EndPos.z );

		var delta = new Vector3( CheckSpacing, CheckSpacing, 0f );
		var mins = Origin - delta;
		var maxs = Origin + delta;

		maxs.z = Origin.z;
		mins.z -= length;

		var list = new BaseEntity[10];
		var count = Util.EntitiesInBox( list, mins, maxs, EntityFlags.Client | EntityFlags.Monster );

		for ( var i = 0; i < count; i++ )
		{
			if ( list[i] != this && Relationship( list[i] ) > RelationshipNone && list[i].DeadFlag == DeadNo )
				return list[i];
		}

		return null;
	}
}
