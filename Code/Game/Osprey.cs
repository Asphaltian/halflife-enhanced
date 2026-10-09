using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_osprey" )]
public class Osprey : BaseMonster
{
	private const int SpawnFlagWaitForTrigger = 0x40;
	private const int MaxCarry = 24;

	[Save( FieldType.ClassPtr )]
	private BaseEntity _goal;
	[Save( FieldType.Vector )]
	private Vector3 _velocity1;
	[Save( FieldType.Vector )]
	private Vector3 _velocity2;
	[Save( FieldType.PositionVector )]
	private Vector3 _position1;
	[Save( FieldType.PositionVector )]
	private Vector3 _position2;
	[Save( FieldType.Vector )]
	private Vector3 _angles1;
	[Save( FieldType.Vector )]
	private Vector3 _angles2;
	[Save( FieldType.Time )]
	private float _startTime;
	[Save( FieldType.Float )]
	private float _deltaTime;

	[Save( FieldType.Vector )]
	private Vector3 _velocity;

	[Save( FieldType.Float )]
	private float _rotorTilt;

	[Save( FieldType.Float )]
	private float _rightHealth;
	[Save( FieldType.Float )]
	private float _leftHealth;

	[Save( FieldType.Integer )]
	private int _units;
	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _grunts = new BaseEntity[MaxCarry];
	[Save( FieldType.PositionVector )]
	private readonly Vector3[] _gruntOrigins = new Vector3[MaxCarry];
	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _repel = new BaseEntity[4];

	private int _soundState;
	private int _spriteTexture;

	private int _pitch;

	private int _explode;
	private int _tailGibs;
	private int _bodyGibs;

	[Save( FieldType.Integer )]
	private int _doLeftSmokePuff;
	[Save( FieldType.Integer )]
	private int _doRightSmokePuff;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override int BloodColor => DontBleed;

	public override int Classify() => ClassMachine;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "models/osprey.mdl" );
		SetSize( new Vector3( -400f, -400f, -100f ), new Vector3( 400f, 400f, 32f ) );
		SetOrigin( Origin );

		EntityFlags |= EntityFlags.Monster;
		DamageMode = DamageYes;
		_rightHealth = 200f;
		_leftHealth = 200f;
		Health = 400f;

		FieldOfView = 0f;

		Sequence = 0;
		ResetSequenceInfo();
		Frame = Server.RandomLong( 0, 0xFF );

		InitBoneControllers();

		SetThink( FindAllThink );
		SetUse( CommandUse );

		if ( (SpawnFlags & SpawnFlagWaitForTrigger) == 0 )
			NextThink = (float)(Server.Globals.Time + 1.0);

		_position2 = Origin;
		_angles2 = Angles;
		_velocity2 = Velocity;
	}

	public override void Precache()
	{
		Util.PrecacheOther( "monster_human_grunt" );

		Server.PrecacheModel( "models/osprey.mdl" );
		Server.PrecacheModel( "models/HVR.mdl" );

		Server.PrecacheSound( "apache/ap_rotor4.wav" );
		Server.PrecacheSound( "weapons/mortarhit.wav" );

		_spriteTexture = Server.PrecacheModel( "sprites/rope.spr" );

		_explode = Server.PrecacheModel( "sprites/fexplo.spr" );
		_tailGibs = Server.PrecacheModel( "models/osprey_tailgibs.mdl" );
		_bodyGibs = Server.PrecacheModel( "models/osprey_bodygibs.mdl" );
		Server.PrecacheModel( "models/osprey_enginegibs.mdl" );
	}

	public void CommandUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void FindAllThink()
	{
		BaseEntity entity = null;

		_units = 0;

		while ( _units < MaxCarry && (entity = Util.FindEntityByClassname( entity, "monster_human_grunt" )) is not null )
		{
			if ( !entity.IsAlive )
				continue;

			_grunts[_units] = entity;
			_gruntOrigins[_units] = entity.Origin;
			_units++;
		}

		if ( _units == 0 )
		{
			Util.Alert( AlertType.Console, "osprey error: no grunts to resupply" );
			Remove();
			return;
		}

		SetThink( FlyThink );
		NextThink = (float)(Server.Globals.Time + 0.1);
		_startTime = Server.Globals.Time;
	}

	public void DeployThink()
	{
		var globals = Server.Globals;

		Util.MakeAimVectors( Angles, globals );

		var forward = globals.Forward;
		var right = globals.Right;
		var up = globals.Up;

		var trace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, -4096f ), IgnoreMonsters, this );

		SoundEnt.InsertSound( WorldSound.Danger, trace.EndPos, 400, 0.3f );

		_repel[0] = MakeGrunt( Origin + (forward * 32f) + (right * 100f) + (up * -96f) );
		_repel[1] = MakeGrunt( Origin + (forward * -64f) + (right * 100f) + (up * -96f) );
		_repel[2] = MakeGrunt( Origin + (forward * 32f) + (right * -100f) + (up * -96f) );
		_repel[3] = MakeGrunt( Origin + (forward * -64f) + (right * -100f) + (up * -96f) );

		SetThink( HoverThink );
		NextThink = (float)(globals.Time + 0.1);
	}

	private bool HasDead()
	{
		for ( var i = 0; i < _units; i++ )
		{
			if ( !_grunts[i].IsValid() || !_grunts[i].IsAlive )
				return true;

			_gruntOrigins[i] = _grunts[i].Origin;
		}

		return false;
	}

	private BaseMonster MakeGrunt( Vector3 source )
	{
		var trace = Util.TraceLine( source, source + new Vector3( 0f, 0f, -4096f ), DontIgnoreMonsters, this );

		if ( trace.Ent is { } hit && hit.Solid != Solid.Bsp )
			return null;

		for ( var i = 0; i < _units; i++ )
		{
			if ( _grunts[i].IsValid() && _grunts[i].IsAlive )
				continue;

			if ( _grunts[i].IsValid() && _grunts[i].RenderMode == RenderMode.Normal )
				_grunts[i].SubStartFadeOut();

			var grunt = (BaseMonster)Create( "monster_human_grunt", source, Angles, null );

			grunt.MoveType = MoveType.Fly;
			grunt.Velocity = new Vector3( 0f, 0f, Server.RandomFloat( -196f, -128f ) );
			grunt.SetActivity( Activity.Glide );

			var beam = Beam.BeamCreate( Server, "sprites/rope.spr", 10 );

			beam.PointEntInit( source + new Vector3( 0f, 0f, 112f ), grunt.Index );
			beam.SetBeamFlags( (int)BeamFlags.Solid );
			beam.SetColor( 255, 255, 255 );
			beam.SetThink( beam.SubRemove );
			beam.NextThink = (float)(Server.Globals.Time + (-4096.0 * trace.Fraction / grunt.Velocity.z) + 0.5);

			grunt.LastPosition = _gruntOrigins[i];
			_grunts[i] = grunt;

			return grunt;
		}

		return null;
	}

	public void HoverThink()
	{
		int i;

		for ( i = 0; i < 4; i++ )
		{
			if ( _repel[i].IsValid() && _repel[i].Health > 0f && (_repel[i].EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
				break;
		}

		if ( i == 4 )
		{
			_startTime = Server.Globals.Time;
			SetThink( FlyThink );
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
		Util.MakeAimVectors( Angles, Server.Globals );
		ShowDamage();
	}

	private void UpdateGoal()
	{
		if ( _goal is null )
		{
			Util.Alert( AlertType.Console, "osprey missing target" );
			return;
		}

		_position1 = _position2;
		_angles1 = _angles2;
		_velocity1 = _velocity2;
		_position2 = _goal.Origin;
		_angles2 = _goal.Angles;
		Util.MakeAimVectors( new Vector3( 0f, _angles2.y, 0f ), Server.Globals );
		_velocity2 = Server.Globals.Forward * _goal.Speed;

		_startTime += _deltaTime;
		_deltaTime = (float)(2.0 * MathLib.Length( _position1 - _position2 ) / (MathLib.Length( _velocity1 ) + _goal.Speed));

		if ( _angles1.y - _angles2.y < -180f )
			_angles1.y += 360f;
		else if ( _angles1.y - _angles2.y > 180f )
			_angles1.y -= 360f;
	}

	public void FlyThink()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		if ( _goal is null && !string.IsNullOrEmpty( Target ) )
		{
			_goal = Util.FindEntityByTargetname( null, Target );
			UpdateGoal();
		}

		if ( Server.Globals.Time > _startTime + _deltaTime )
		{
			if ( _goal.Speed == 0f )
				SetThink( DeployThink );

			do
			{
				_goal = Util.FindEntityByTargetname( null, _goal.Target );
			}
			while ( _goal.Speed < 400f && !HasDead() );

			UpdateGoal();
		}

		Flight();
		ShowDamage();
	}

	private void Flight()
	{
		var time = Server.Globals.Time - _startTime;
		var scale = (float)(1.0 / _deltaTime);

		var fraction = Util.SplineFraction( time * scale, 1f );
		var inverse = (float)(1.0 - fraction);

		var position = ((_position1 + (_velocity1 * time)) * inverse) + ((_position2 - (_velocity2 * (_deltaTime - time))) * fraction);
		var angles = (_angles1 * inverse) + (_angles2 * fraction);

		_velocity = (_velocity1 * inverse) + (_velocity2 * fraction);

		SetOrigin( position );
		Angles = angles;
		Util.MakeAimVectors( Angles, Server.Globals );

		var speed = Vector3.Dot( Server.Globals.Forward, _velocity );
		var idealTilt = (float)((160 - speed) / 10.0);

		if ( _rotorTilt < idealTilt )
		{
			_rotorTilt += 0.5f;

			if ( _rotorTilt > 0f )
				_rotorTilt = 0f;
		}

		if ( _rotorTilt > idealTilt )
		{
			_rotorTilt -= 0.5f;

			if ( _rotorTilt < -90f )
				_rotorTilt = -90f;
		}

		SetBoneController( 0, _rotorTilt );

		if ( _soundState == 0 )
		{
			Sound.EmitDyn( this, SoundChannel.Static, "apache/ap_rotor4.wav", 1f, 0.15f, 0, 110 );

			_soundState = Server.SoundChangePitch;
			return;
		}

		if ( Util.FindEntityByClassname( null, "player" ) is not { } player )
			return;

		var pitch = Vector3.Dot( _velocity - player.Velocity, Util.Normalize( player.Origin - Origin ) );

		pitch = (int)(100 + (pitch / 75.0));

		if ( pitch > 250f )
			pitch = 250f;

		if ( pitch < 50f )
			pitch = 50f;

		if ( pitch == 100f )
			pitch = 101f;

		if ( pitch == _pitch )
			return;

		_pitch = (int)pitch;
		Sound.EmitDyn( this, SoundChannel.Static, "apache/ap_rotor4.wav", 1f, 0.15f, Server.SoundChangePitch | Server.SoundChangeVolume, (int)pitch );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		MoveType = MoveType.Toss;
		Gravity = 0.3f;
		Velocity = _velocity;
		AVelocity = new Vector3( Server.RandomFloat( -20f, 20f ), 0f, Server.RandomFloat( -50f, 50f ) );
		Sound.Stop( this, SoundChannel.Static, "apache/ap_rotor4.wav" );

		SetSize( new Vector3( -32f, -32f, -64f ), new Vector3( 32f, 32f, 0f ) );
		SetThink( DyingThink );
		SetTouch( CrashTouch );
		NextThink = (float)(Server.Globals.Time + 0.1);
		Health = 0f;
		DamageMode = DamageNo;

		_startTime = Server.Globals.Time + 4f;
	}

	public void CrashTouch( BaseEntity other )
	{
		if ( other.Solid != Solid.Bsp )
			return;

		SetTouch( null );
		_startTime = Server.Globals.Time;
		NextThink = Server.Globals.Time;
		_velocity = Velocity;
	}

	public void DyingThink()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		AVelocity *= 1.02f;

		Vector3 spot;

		if ( _startTime > Server.Globals.Time )
		{
			Util.MakeAimVectors( Angles, Server.Globals );
			ShowDamage();

			spot = Origin + (Velocity * 0.2f);

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
			Server.WriteByte( TempEntity.Explosion );
			Server.WriteCoord( spot.x + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( spot.y + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( spot.z + Server.RandomFloat( -150f, -50f ) );
			Server.WriteShort( GameModels.Fireball );
			Server.WriteByte( Server.RandomLong( 0, 29 ) + 30 );
			Server.WriteByte( 12 );
			Server.WriteByte( TempEntity.ExplosionNone );
			Server.MessageEnd();

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
			Server.WriteByte( TempEntity.Smoke );
			Server.WriteCoord( spot.x + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( spot.y + Server.RandomFloat( -150f, 150f ) );
			Server.WriteCoord( spot.z + Server.RandomFloat( -150f, -50f ) );
			Server.WriteShort( GameModels.Smoke );
			Server.WriteByte( 100 );
			Server.WriteByte( 10 );
			Server.MessageEnd();

			spot = Origin + ((Mins + Maxs) * 0.5f);

			Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
			Server.WriteByte( TempEntity.BreakModel );
			Server.WriteCoord( spot.x );
			Server.WriteCoord( spot.y );
			Server.WriteCoord( spot.z );
			Server.WriteCoord( 800f );
			Server.WriteCoord( 800f );
			Server.WriteCoord( 132f );
			Server.WriteCoord( Velocity.x );
			Server.WriteCoord( Velocity.y );
			Server.WriteCoord( Velocity.z );
			Server.WriteByte( 50 );
			Server.WriteShort( _tailGibs );
			Server.WriteByte( 8 );
			Server.WriteByte( 200 );
			Server.WriteByte( Breakable.BreakMetal );
			Server.MessageEnd();

			EntityFlags &= ~EntityFlags.OnGround;
			NextThink = (float)(Server.Globals.Time + 0.2);
			return;
		}

		spot = Origin + ((Mins + Maxs) * 0.5f);

		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.Sprite );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z + 512f );
		Server.WriteShort( _explode );
		Server.WriteByte( 250 );
		Server.WriteByte( 255 );
		Server.MessageEnd();

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.BeamCylinder );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z + 2000f );
		Server.WriteShort( _spriteTexture );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 4 );
		Server.WriteByte( 32 );
		Server.WriteByte( 0 );
		Server.WriteByte( 255 );
		Server.WriteByte( 255 );
		Server.WriteByte( 192 );
		Server.WriteByte( 128 );
		Server.WriteByte( 0 );
		Server.MessageEnd();

		Sound.Emit( this, SoundChannel.Static, "weapons/mortarhit.wav", 1f, 0.3f );

		RadiusDamage( Origin, this, this, 300f, ClassNone, DamageBlast );

		spot = Origin + ((Mins + Maxs) * 0.5f);

		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, spot );
		Server.WriteByte( TempEntity.BreakModel );
		Server.WriteCoord( spot.x );
		Server.WriteCoord( spot.y );
		Server.WriteCoord( spot.z + 64f );
		Server.WriteCoord( 800f );
		Server.WriteCoord( 800f );
		Server.WriteCoord( 128f );
		Server.WriteCoord( _velocity.x );
		Server.WriteCoord( _velocity.y );
		Server.WriteCoord( (float)(Math.Abs( _velocity.z ) * 0.25) );
		Server.WriteByte( 40 );
		Server.WriteShort( _bodyGibs );
		Server.WriteByte( 128 );
		Server.WriteByte( 200 );
		Server.WriteByte( Breakable.BreakMetal );
		Server.MessageEnd();

		Remove();
	}

	private void ShowDamage()
	{
		if ( _doLeftSmokePuff > 0 || Server.RandomLong( 0, 99 ) > _leftHealth )
		{
			SmokePuff( Origin + (Server.Globals.Right * -340f) );

			if ( _doLeftSmokePuff > 0 )
				_doLeftSmokePuff--;
		}

		if ( _doRightSmokePuff > 0 || Server.RandomLong( 0, 99 ) > _rightHealth )
		{
			SmokePuff( Origin + (Server.Globals.Right * 340f) );

			if ( _doRightSmokePuff > 0 )
				_doRightSmokePuff--;
		}
	}

	private void SmokePuff( Vector3 source )
	{
		Server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, source );
		Server.WriteByte( TempEntity.Smoke );
		Server.WriteCoord( source.x );
		Server.WriteCoord( source.y );
		Server.WriteCoord( source.z );
		Server.WriteShort( GameModels.Smoke );
		Server.WriteByte( Server.RandomLong( 0, 9 ) + 20 );
		Server.WriteByte( 12 );
		Server.MessageEnd();
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( trace.HitGroup == 3 )
		{
			if ( _rightHealth < 0f )
				return;

			_rightHealth -= damage;
			_doLeftSmokePuff = (int)(3 + (damage / 5.0));
		}

		if ( trace.HitGroup == 2 )
		{
			if ( _leftHealth < 0f )
				return;

			_leftHealth -= damage;
			_doRightSmokePuff = (int)(3 + (damage / 5.0));
		}

		if ( damage > 50f || trace.HitGroup == 1 || trace.HitGroup == 2 || trace.HitGroup == 3 )
			Combat.AddMultiDamage( attacker, this, damage, damageType );
		else
			Util.Sparks( trace.EndPos );
	}
}
