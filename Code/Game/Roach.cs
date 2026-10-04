using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_cockroach" )]
public class Roach : BaseMonster
{
	private const int ModeIdle = 0;
	private const int ModeBored = 1;
	private const int ModeScaredByEntity = 2;
	private const int ModeScaredByLight = 3;
	private const int ModeSmellFood = 4;
	private const int ModeEat = 5;

	private float _lastLightLevel;
	private float _nextSmellTime;
	private bool _lightHacked;
	private int _mode;

	public override int SoundMask() => WorldSound.Carcass | WorldSound.Meat;

	public override int Classify() => ClassInsect;

	public override void Touch( BaseEntity other )
	{
		if ( MathLib.VectorCompare( other.Velocity, Vector3.Zero ) || !other.IsPlayer )
			return;

		var spot = Origin + new Vector3( 0f, 0f, 8f );
		var trace = Util.TraceLine( spot, spot + new Vector3( 0f, 0f, -24f ), IgnoreMonsters, this );

		Util.DecalTrace( trace, Decals.YellowBlood1 + Server.RandomLong( 0, 5 ) );

		TakeDamage( other, other, Health, DamageCrush );
	}

	public override void SetYawSpeed() => YawSpeed = 120f;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/roach.mdl" );
		SetSize( new Vector3( -1f, -1f, 0f ), new Vector3( 1f, 1f, 2f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorYellow;
		Effects = 0;
		Health = 1f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();
		SetActivity( Activity.Idle );

		ViewOffset = new Vector3( 0f, 0f, 1f );
		DamageMode = DamageYes;
		_lightHacked = false;
		_lastLightLevel = -1f;
		_mode = ModeIdle;
		_nextSmellTime = Server.Globals.Time;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/roach.mdl" );

		Server.PrecacheSound( "roach/rch_die.wav" );
		Server.PrecacheSound( "roach/rch_walk.wav" );
		Server.PrecacheSound( "roach/rch_smash.wav" );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		Solid = Solid.Not;

		if ( Server.RandomLong( 0, 4 ) == 1 )
			Sound.EmitDyn( this, SoundChannel.Voice, "roach/rch_die.wav", 0.8f, Server.AttenuationNormal, 0, 80 + Server.RandomLong( 0, 39 ) );
		else
			Sound.EmitDyn( this, SoundChannel.Body, "roach/rch_smash.wav", 0.7f, Server.AttenuationNormal, 0, 80 + Server.RandomLong( 0, 39 ) );

		SoundEnt.InsertSound( WorldSound.World, Origin, 128, 1f );

		if ( Owner is BaseEntity owner )
			owner.DeathNotice( this );

		Remove();
	}

	public override void MonsterThink()
	{
		if ( Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
			NextThink = Server.Globals.Time + Server.RandomFloat( 1f, 1.5f );
		else
			NextThink = (float)(Server.Globals.Time + 0.1);

		var interval = StudioFrameAdvance();

		if ( !_lightHacked )
		{
			NextThink = Server.Globals.Time + 1f;
			_lightHacked = true;
			return;
		}

		if ( _lastLightLevel < 0f )
			_lastLightLevel = Server.GetEntityIllum( this );

		switch ( _mode )
		{
			case ModeIdle:
			case ModeEat:
				if ( Server.RandomLong( 0, 3 ) == 1 )
				{
					Look( 150 );

					if ( HasConditions( ConditionSeeFear ) )
					{
						Eat( 30 + Server.RandomLong( 0, 14 ) );

						PickNewDest( ModeScaredByEntity );
						SetActivity( Activity.Walk );
					}
					else if ( Server.RandomLong( 0, 149 ) == 1 )
					{
						PickNewDest( ModeBored );
						SetActivity( Activity.Walk );

						if ( _mode == ModeEat )
							Eat( 30 + Server.RandomLong( 0, 14 ) );
					}
				}

				if ( _mode == ModeIdle )
				{
					if ( ShouldEat() )
						Listen();

					if ( Server.GetEntityIllum( this ) > _lastLightLevel )
					{
						PickNewDest( ModeScaredByLight );
						SetActivity( Activity.Walk );
					}
					else if ( HasConditions( ConditionSmellFood )
						&& SoundEnt.SoundPointerForIndex( AudibleList ) is { } sound
						&& Math.Abs( sound.Origin.z - Origin.z ) <= 3f )
					{
						PickNewDest( ModeSmellFood );
						SetActivity( Activity.Walk );
					}
				}

				break;

			case ModeScaredByLight:
				if ( Server.GetEntityIllum( this ) <= _lastLightLevel )
				{
					SetActivity( Activity.Idle );
					_lastLightLevel = Server.GetEntityIllum( this );
				}

				break;
		}

		if ( GroundSpeed != 0f )
			Move( interval );
	}

	public void PickNewDest( int condition )
	{
		_mode = condition;

		if ( _mode == ModeSmellFood && SoundEnt.SoundPointerForIndex( AudibleList ) is { } sound )
		{
			Route[0].Location.x = sound.Origin.x + (3 - Server.RandomLong( 0, 5 ));
			Route[0].Location.y = sound.Origin.y + (3 - Server.RandomLong( 0, 5 ));
			Route[0].Location.z = sound.Origin.z;
			Route[0].Type = MoveFlag.ToLocation;
			MovementGoal = RouteClassify( Route[0].Type );
			return;
		}

		Vector3 destination;

		do
		{
			var direction = new Vector3( Server.RandomFloat( -1f, 1f ), Server.RandomFloat( -1f, 1f ), 0f );
			float distance = 256 + Server.RandomLong( 0, 255 );

			destination = Origin + (direction * distance);
		}
		while ( MathLib.Length2D( destination - Origin ) < 128f );

		Route[0].Location.x = destination.x;
		Route[0].Location.y = destination.y;
		Route[0].Location.z = Origin.z;
		Route[0].Type = MoveFlag.ToLocation;
		MovementGoal = RouteClassify( Route[0].Type );

		if ( Server.RandomLong( 0, 9 ) == 1 )
			Sound.EmitDyn( this, SoundChannel.Body, "roach/rch_walk.wav", 1f, Server.AttenuationNormal, 0, 80 + Server.RandomLong( 0, 39 ) );
	}

	public override void Move( float interval = 0.1f )
	{
		var waypointDistance = MathLib.Length2D( Route[RouteIndex].Location - Origin );

		MakeIdealYaw( Route[RouteIndex].Location );
		ChangeYaw( (int)YawSpeed );
		Server.Globals.MakeVectors( Angles );

		if ( Server.RandomLong( 0, 7 ) == 1 && !Server.WalkMove( this, IdealYaw, 4f, WalkMoveMode.Normal ) )
			PickNewDest( _mode );

		Server.WalkMove( this, IdealYaw, GroundSpeed * interval, WalkMoveMode.Normal );

		if ( waypointDistance <= GroundSpeed * interval )
		{
			SetActivity( Activity.Idle );
			_lastLightLevel = Server.GetEntityIllum( this );

			_mode = _mode == ModeSmellFood ? ModeEat : ModeIdle;
		}

		if ( Server.RandomLong( 0, 149 ) == 1 && _mode != ModeScaredByLight && _mode != ModeSmellFood )
			PickNewDest( ModeIdle );
	}

	public override void Look( int distance )
	{
		var sighted = 0;

		ClearConditions( ConditionSeeHate | ConditionSeeDislike | ConditionSeeEnemy | ConditionSeeFear );

		if ( Util.IsNullEnt( Server.FindClientInPvs( this ) ) )
			return;

		NextLink = null;

		BaseEntity previous = this;
		BaseEntity sight = null;

		while ( (sight = Util.FindEntityInSphere( sight, Origin, distance )) is not null )
		{
			if ( !sight.IsPlayer && (sight.EntityFlags & EntityFlags.Monster) != EntityFlags.Monster )
				continue;

			if ( (sight.EntityFlags & EntityFlags.NoTarget) == EntityFlags.NoTarget || sight.Health <= 0f )
				continue;

			previous.NextLink = sight;
			sight.NextLink = null;
			previous = sight;

			switch ( Relationship( sight ) )
			{
				case RelationshipFear:
					sighted |= ConditionSeeFear;
					break;

				case RelationshipNone:
					break;

				default:
					Log.Info( $"{ClassName} can't asses {sight.ClassName}" );
					break;
			}
		}

		SetConditions( sighted );
	}
}
