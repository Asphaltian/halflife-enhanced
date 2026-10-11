using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Server
{
	private const int MaxClipPlanes = 5;
	private const float StopEpsilon = 0.1f;

	private static readonly Vector3[] CurrentTable =
	[
		new( 1f, 0f, 0f ),
		new( 0f, 1f, 0f ),
		new( -1f, 0f, 0f ),
		new( 0f, -1f, 0f ),
		new( 0f, 0f, 1f ),
		new( 0f, 0f, -1f )
	];

	public int PointContents( Vector3 point )
	{
		var contents = WorldBrushModel.Hulls[0].ServerPointContents( 0, point );

		if ( contents is <= (int)Contents.Current0 and >= (int)Contents.CurrentDown )
			contents = (int)Contents.Water;
		else if ( contents == (int)Contents.Solid )
			return contents;

		var entityContents = LinkContents( _areaNodes, point );
		return entityContents != -1 ? entityContents : contents;
	}

	private int LinkContents( AreaNode node, Vector3 point )
	{
		while ( true )
		{
			foreach ( var touch in node.SolidEdicts )
			{
				if ( touch.Solid != Solid.Not || GetModel( touch.ModelIndex ) is null )
					continue;

				if ( point.x > touch.AbsMax.x || point.y > touch.AbsMax.y || point.z > touch.AbsMax.z )
					continue;

				if ( point.x < touch.AbsMin.x || point.y < touch.AbsMin.y || point.z < touch.AbsMin.z )
					continue;

				if ( touch.Skin is < -100 or > 100 )
					Log.Info( $"Invalid contents on trigger field: {touch.ClassName}" );

				var hull = HullForBsp( touch, Vector3.Zero, Vector3.Zero, out var offset );

				if ( hull.ServerPointContents( hull.FirstClipNode, point - offset ) != (int)Contents.Empty )
					return touch.Skin;
			}

			if ( node.Axis == -1 )
				return -1;

			if ( point[node.Axis] > node.Dist )
				node = node.Children[0];
			else if ( point[node.Axis] < node.Dist )
				node = node.Children[1];
			else
				return -1;
		}
	}

	private static bool IsLiquid( int contents )
	{
		return contents is <= (int)Contents.Water and > (int)Contents.Translucent;
	}

	private bool CheckWater( Edict edict )
	{
		var point = new Vector3( (edict.AbsMax.x + edict.AbsMin.x) * 0.5f, (edict.AbsMax.y + edict.AbsMin.y) * 0.5f, edict.AbsMin.z + 1f );

		edict.WaterLevel = 0;
		edict.WaterType = (int)Contents.Empty;

		var contents = PointContents( point );
		if ( !IsLiquid( contents ) )
			return false;

		edict.WaterType = contents;
		edict.WaterLevel = MeasureWaterLevel( edict, point );

		if ( contents is <= (int)Contents.Current0 and >= (int)Contents.CurrentDown )
			edict.BaseVelocity += (edict.WaterLevel * 50f) * CurrentTable[(int)Contents.Current0 - contents];

		return edict.WaterLevel > 1;
	}

	private int MeasureWaterLevel( Edict edict, Vector3 point )
	{
		if ( edict.AbsMin.z == edict.AbsMax.z )
			return 3;

		point.z = (edict.AbsMax.z + edict.AbsMin.z) * 0.5f;

		if ( !IsLiquid( PointContents( point ) ) )
			return 1;

		point += edict.ViewOffset;

		return IsLiquid( PointContents( point ) ) ? 3 : 2;
	}

	private void CheckWaterTransition( Edict edict )
	{
		var point = new Vector3( (edict.AbsMax.x + edict.AbsMin.x) * 0.5f, (edict.AbsMax.y + edict.AbsMin.y) * 0.5f, edict.AbsMin.z + 1f );
		var contents = PointContents( point );

		if ( edict.WaterType == 0 )
		{
			edict.WaterType = contents;
			edict.WaterLevel = 1;
			return;
		}

		if ( !IsLiquid( contents ) )
		{
			if ( edict.WaterType != (int)Contents.Empty )
				EmitSound( edict, SoundChannel.Auto, "player/pl_wade2.wav", 1f, 1f );

			edict.WaterType = (int)Contents.Empty;
			edict.WaterLevel = 0;
			return;
		}

		if ( edict.WaterType == (int)Contents.Empty )
		{
			EmitSound( edict, SoundChannel.Auto, "player/pl_wade1.wav", 1f, 1f );
			edict.Velocity = new Vector3( edict.Velocity.x, edict.Velocity.y, edict.Velocity.z * 0.5f );
		}

		edict.WaterType = contents;
		edict.WaterLevel = MeasureWaterLevel( edict, point );
	}

	private void CheckVelocity( Edict edict )
	{
		var velocity = edict.Velocity;
		var origin = edict.Origin;
		var maxVelocity = SvMaxVelocity;

		for ( var i = 0; i < 3; i++ )
		{
			if ( !float.IsFinite( velocity[i] ) )
			{
				Log.Info( $"Got a NaN velocity on {edict.ClassName}" );
				velocity[i] = 0f;
			}

			if ( !float.IsFinite( origin[i] ) )
			{
				Log.Info( $"Got a NaN origin on {edict.ClassName}" );
				origin[i] = 0f;
			}

			if ( velocity[i] > maxVelocity )
			{
				Log.Trace( $"Got a velocity too high on {edict.ClassName}" );
				velocity[i] = maxVelocity;
			}
			else if ( velocity[i] < -maxVelocity )
			{
				Log.Trace( $"Got a velocity too low on {edict.ClassName}" );
				velocity[i] = -maxVelocity;
			}
		}

		edict.Velocity = velocity;
		edict.Origin = origin;
	}

	private void AddGravity( Edict edict )
	{
		var gravity = edict.Gravity != 0f ? edict.Gravity : 1f;

		var velocity = edict.Velocity;
		velocity.z = (float)(velocity.z - ((double)(gravity * SvGravity) * FrameTime));
		velocity.z = (float)(((double)edict.BaseVelocity.z * FrameTime) + velocity.z);

		edict.Velocity = velocity;
		edict.BaseVelocity = new Vector3( edict.BaseVelocity.x, edict.BaseVelocity.y, 0f );

		CheckVelocity( edict );
	}

	private static int ClipVelocity( Vector3 input, Vector3 normal, out Vector3 output, float overbounce )
	{
		var blocked = 0;

		if ( normal.z > 0f )
			blocked |= 1;

		if ( normal.z == 0f )
			blocked |= 2;

		var backoff = Vector3.Dot( input, normal ) * overbounce;

		output = default;

		for ( var i = 0; i < 3; i++ )
		{
			output[i] = input[i] - (normal[i] * backoff);

			if ( output[i] > -StopEpsilon && output[i] < StopEpsilon )
				output[i] = 0f;
		}

		return blocked;
	}

	private void PhysicsToss( Edict edict )
	{
		CheckWater( edict );

		if ( !RunThink( edict ) )
			return;

		if ( edict.Velocity.z > 0f || edict.GroundEntity is null || (edict.GroundEntity.EntityFlags & (EntityFlags.Monster | EntityFlags.Client)) != 0 )
			edict.EntityFlags &= ~EntityFlags.OnGround;

		if ( (edict.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && IsZero( edict.Velocity ) )
		{
			edict.AVelocity = Vector3.Zero;

			if ( IsZero( edict.BaseVelocity ) )
				return;
		}

		CheckVelocity( edict );

		if ( edict.MoveType is not (MoveType.Fly or MoveType.BounceMissile or MoveType.FlyMissile) )
			AddGravity( edict );

		edict.Angles += (float)FrameTime * edict.AVelocity;

		edict.Velocity += edict.BaseVelocity;
		CheckVelocity( edict );

		var move = edict.Velocity * (float)FrameTime;
		edict.Velocity -= edict.BaseVelocity;

		var trace = PushEntity( edict, move );
		CheckVelocity( edict );

		if ( trace.AllSolid )
		{
			edict.Velocity = Vector3.Zero;
			edict.AVelocity = Vector3.Zero;
			return;
		}

		if ( trace.Fraction != 1f )
		{
			if ( !IsInUse( edict ) )
				return;

			var backoff = edict.MoveType switch
			{
				MoveType.Bounce => 2f - edict.Friction,
				MoveType.BounceMissile => 2f,
				_ => 1f
			};

			ClipVelocity( edict.Velocity, trace.PlaneNormal, out var clipped, backoff );
			edict.Velocity = clipped;

			if ( trace.PlaneNormal.z > 0.7f )
			{
				move = edict.Velocity + edict.BaseVelocity;

				if ( move.z < (double)SvGravity * FrameTime )
				{
					edict.EntityFlags |= EntityFlags.OnGround;
					edict.GroundEntity = trace.Ent;
					edict.Velocity = new Vector3( edict.Velocity.x, edict.Velocity.y, 0f );
				}

				if ( Vector3.Dot( move, move ) < 900f || edict.MoveType is not (MoveType.Bounce or MoveType.BounceMissile) )
				{
					edict.EntityFlags |= EntityFlags.OnGround;
					edict.GroundEntity = trace.Ent;
					edict.Velocity = Vector3.Zero;
					edict.AVelocity = Vector3.Zero;
				}
				else
				{
					var scale = (float)((1.0 - trace.Fraction) * FrameTime * 0.9);

					move = (edict.Velocity * scale) + (scale * edict.BaseVelocity);
					PushEntity( edict, move );
				}
			}
		}

		CheckWaterTransition( edict );
	}

	private void PhysicsStep( Edict edict )
	{
		WaterMove( edict );
		CheckVelocity( edict );

		var wasOnGround = (edict.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround;
		var inWater = CheckWater( edict );

		if ( (edict.EntityFlags & EntityFlags.Float) == EntityFlags.Float && edict.WaterLevel > 0 )
		{
			var buoyancy = (float)((double)(edict.Skin * Submerged( edict )) * FrameTime);

			AddGravity( edict );
			edict.Velocity += new Vector3( 0f, 0f, buoyancy );
		}

		if ( !wasOnGround
			&& (edict.EntityFlags & EntityFlags.Fly) != EntityFlags.Fly
			&& !((edict.EntityFlags & EntityFlags.Swim) == EntityFlags.Swim && edict.WaterLevel > 0)
			&& !inWater )
		{
			AddGravity( edict );
		}

		if ( !IsZero( edict.Velocity ) || !IsZero( edict.BaseVelocity ) )
		{
			edict.EntityFlags &= ~EntityFlags.OnGround;

			if ( wasOnGround && (edict.Health > 0f || CheckBottom( edict )) )
				ApplyStepFriction( edict );

			edict.Velocity += edict.BaseVelocity;
			CheckVelocity( edict );

			FlyMove( edict, (float)FrameTime );
			CheckVelocity( edict );

			edict.Velocity -= edict.BaseVelocity;
			CheckVelocity( edict );

			var mins = edict.Origin + edict.Mins;
			var maxs = edict.Origin + edict.Maxs;

			for ( var x = 0; x <= 1; x++ )
			{
				for ( var y = 0; y <= 1; y++ )
				{
					var point = new Vector3( x != 0 ? maxs.x : mins.x, y != 0 ? maxs.y : mins.y, mins.z - 1f );

					if ( PointContents( point ) == (int)Contents.Solid )
					{
						edict.EntityFlags |= EntityFlags.OnGround;
						break;
					}
				}
			}

			LinkEdict( edict, true );
		}
		else if ( Globals.ForceRetouch != 0f )
		{
			var trace = Move( edict.Origin, edict.Mins, edict.Maxs, edict.Origin, (int)MoveKind.Normal, edict, (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip );

			if ( (trace.Fraction < 1f || trace.StartSolid) && trace.Ent is not null )
				Impact( edict, trace.Ent, trace );
		}

		RunThink( edict );
		CheckWaterTransition( edict );
	}

	private void ApplyStepFriction( Edict edict )
	{
		var velocity = edict.Velocity;
		var speed = (float)Math.Sqrt( (velocity.x * velocity.x) + (velocity.y * velocity.y) );

		if ( speed == 0f )
			return;

		var friction = SvFriction * edict.Friction;
		edict.Friction = 1f;

		var control = speed < SvStopSpeed ? SvStopSpeed : speed;
		var newSpeed = (float)(speed - ((double)control * FrameTime * friction));

		if ( newSpeed < 0f )
			newSpeed = 0f;

		newSpeed /= speed;

		edict.Velocity = new Vector3( velocity.x * newSpeed, velocity.y * newSpeed, velocity.z );
	}

	private float Submerged( Edict edict )
	{
		var center = (edict.AbsMin + edict.AbsMax) * 0.5f;
		var bottom = edict.AbsMin.z - center.z;

		switch ( edict.WaterLevel )
		{
			case 1:
				return RecursiveWaterLevel( center, 0f, bottom, 0 ) - bottom;

			case 3:
				if ( PointContents( new Vector3( center.x, center.y, edict.AbsMax.z ) ) == (int)Contents.Water )
					return edict.Maxs.z - edict.Mins.z;

				goto case 2;

			case 2:
				return RecursiveWaterLevel( center, edict.AbsMax.z - center.z, 0f, 0 ) - bottom;

			default:
				return 0f;
		}
	}

	private float RecursiveWaterLevel( Vector3 center, float outside, float inside, int count )
	{
		var offset = ((outside - inside) * 0.5f) + inside;

		if ( ++count > 5 )
			return offset;

		var test = new Vector3( center.x, center.y, center.z + offset );

		return PointContents( test ) == (int)Contents.Water
			? RecursiveWaterLevel( center, outside, offset, count )
			: RecursiveWaterLevel( center, offset, inside, count );
	}

	private bool CheckBottom( Edict edict )
	{
		var mins = edict.Origin + edict.Mins;
		var maxs = edict.Origin + edict.Maxs;
		var monsterClip = (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip;

		var allSolid = true;

		for ( var x = 0; x <= 1 && allSolid; x++ )
		{
			for ( var y = 0; y <= 1 && allSolid; y++ )
			{
				var corner = new Vector3( x != 0 ? maxs.x : mins.x, y != 0 ? maxs.y : mins.y, mins.z - 1f );
				allSolid = PointContents( corner ) == (int)Contents.Solid;
			}
		}

		if ( allSolid )
			return true;

		var stepSize = SvStepSize;

		var start = new Vector3( (mins.x + maxs.x) * 0.5f, (mins.y + maxs.y) * 0.5f, mins.z + stepSize );
		var stop = new Vector3( start.x, start.y, start.z - (stepSize * 2f) );

		var trace = Move( start, Vector3.Zero, Vector3.Zero, stop, (int)MoveKind.NoMonsters, edict, monsterClip );
		if ( trace.Fraction == 1f )
			return false;

		var mid = trace.EndPos.z;

		for ( var x = 0; x <= 1; x++ )
		{
			for ( var y = 0; y <= 1; y++ )
			{
				start.x = stop.x = x != 0 ? maxs.x : mins.x;
				start.y = stop.y = y != 0 ? maxs.y : mins.y;

				trace = Move( start, Vector3.Zero, Vector3.Zero, stop, (int)MoveKind.NoMonsters, edict, monsterClip );

				if ( trace.Fraction == 1f || mid - trace.EndPos.z > stepSize )
					return false;
			}
		}

		return true;
	}

	private int FlyMove( Edict edict, float time )
	{
		var blocked = 0;
		var numPlanes = 0;
		var planes = new Vector3[MaxClipPlanes];

		var originalVelocity = edict.Velocity;
		var primalVelocity = edict.Velocity;
		var monsterClip = (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip;

		for ( var bumpCount = 0; bumpCount < 4; bumpCount++ )
		{
			if ( IsZero( edict.Velocity ) )
				return blocked;

			var end = edict.Origin + (edict.Velocity * time);
			var trace = Move( edict.Origin, edict.Mins, edict.Maxs, end, (int)MoveKind.Normal, edict, monsterClip );

			if ( trace.AllSolid )
			{
				edict.Velocity = Vector3.Zero;
				return 4;
			}

			if ( trace.Fraction > 0f )
			{
				var test = Move( trace.EndPos, edict.Mins, edict.Maxs, trace.EndPos, (int)MoveKind.Normal, edict, monsterClip );

				if ( !test.AllSolid )
				{
					edict.Origin = trace.EndPos;
					originalVelocity = edict.Velocity;
					numPlanes = 0;
				}
			}

			if ( trace.Fraction == 1f )
				return blocked;

			if ( trace.Ent is null )
				throw new InvalidOperationException( "SV_FlyMove: !trace.ent" );

			if ( trace.PlaneNormal.z > 0.7f )
			{
				blocked |= 1;

				if ( trace.Ent.Solid is Solid.Bsp or Solid.SlideBox || trace.Ent.MoveType == MoveType.PushStep || (edict.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
				{
					edict.EntityFlags |= EntityFlags.OnGround;
					edict.GroundEntity = trace.Ent;
				}
			}

			if ( trace.PlaneNormal.z == 0f )
				blocked |= 2;

			Impact( edict, trace.Ent, trace );

			if ( !IsInUse( edict ) )
				return blocked;

			time -= trace.Fraction * time;

			if ( numPlanes >= MaxClipPlanes )
			{
				edict.Velocity = Vector3.Zero;
				return blocked;
			}

			planes[numPlanes++] = trace.PlaneNormal;

			Vector3 newVelocity;

			if ( numPlanes == 1 && edict.MoveType == MoveType.Walk && ((edict.EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround || edict.Friction != 1f) )
			{
				var overbounce = planes[0].z > 0.7f ? 1f : ((1f - edict.Friction) * SvBounce) + 1f;

				ClipVelocity( originalVelocity, planes[0], out newVelocity, overbounce );

				edict.Velocity = newVelocity;
				originalVelocity = newVelocity;
				continue;
			}

			int i;
			newVelocity = default;

			for ( i = 0; i < numPlanes; i++ )
			{
				ClipVelocity( originalVelocity, planes[i], out newVelocity, 1f );

				int j;

				for ( j = 0; j < numPlanes; j++ )
				{
					if ( j != i && Vector3.Dot( newVelocity, planes[j] ) < 0f )
						break;
				}

				if ( j == numPlanes )
					break;
			}

			if ( i != numPlanes )
			{
				edict.Velocity = newVelocity;
			}
			else
			{
				if ( numPlanes != 2 )
					return blocked;

				var dir = MathLib.CrossProduct( planes[0], planes[1] );
				edict.Velocity = dir * Vector3.Dot( dir, edict.Velocity );
			}

			if ( Vector3.Dot( edict.Velocity, primalVelocity ) <= 0f )
			{
				edict.Velocity = Vector3.Zero;
				return blocked;
			}
		}

		return blocked;
	}

	private void WaterMove( Edict edict )
	{
		const EntityFlags immuneWater = EntityFlags.ImmuneWater | EntityFlags.GodMode;
		const EntityFlags immuneLava = EntityFlags.ImmuneLava | EntityFlags.GodMode;
		const EntityFlags immuneSlime = EntityFlags.ImmuneSlime | EntityFlags.GodMode;

		if ( edict.MoveType == MoveType.NoClip )
		{
			edict.AirFinished = (float)(Time + 12.0);
			return;
		}

		if ( edict.Health < 0f )
			return;

		var drownLevel = edict.DeadFlag == 0 ? 3f : 1f;
		var flags = edict.EntityFlags;
		var waterType = edict.WaterType;
		float waterLevel = edict.WaterLevel;

		if ( (flags & immuneWater) == 0 )
		{
			if ( ((flags & EntityFlags.Swim) == EntityFlags.Swim && waterLevel < drownLevel) || waterLevel >= drownLevel )
			{
				if ( edict.AirFinished < Time && edict.PainFinished < Time )
				{
					edict.Damage += 2f;

					if ( edict.Damage > 15f )
						edict.Damage = 10f;

					edict.PainFinished = (float)(Time + 1.0);
				}
			}
			else
			{
				edict.Damage = 2f;
				edict.AirFinished = (float)(Time + 12.0);
			}
		}

		if ( edict.WaterLevel == 0 )
		{
			if ( (flags & EntityFlags.InWater) == EntityFlags.InWater )
			{
				PlayWadeSound( edict );
				edict.EntityFlags = flags & ~EntityFlags.InWater;
			}

			edict.AirFinished = (float)(Time + 12.0);
			return;
		}

		if ( waterType == (int)Contents.Lava )
		{
			if ( (flags & immuneLava) == 0 && edict.DamageTime < Time )
				edict.DamageTime = (float)(Time + (edict.RadSuitFinished < Time ? 0.2 : 1.0));
		}
		else if ( waterType == (int)Contents.Slime && (flags & immuneSlime) == 0 && edict.DamageTime < Time && edict.RadSuitFinished < Time )
		{
			edict.DamageTime = (float)(Time + 1.0);
		}

		if ( (flags & EntityFlags.InWater) != EntityFlags.InWater )
		{
			if ( waterType == (int)Contents.Water )
				PlayWadeSound( edict );

			edict.DamageTime = 0f;
			edict.EntityFlags = flags | EntityFlags.InWater;
		}

		if ( (flags & EntityFlags.WaterJump) != EntityFlags.WaterJump )
			edict.Velocity += (float)(edict.WaterLevel * -0.8 * FrameTime) * edict.Velocity;
	}

	private void PlayWadeSound( Edict edict )
	{
		EmitSound( edict, SoundChannel.Body, $"player/pl_wade{Server.RandomLong( 0, 3 ) + 1}.wav", 1f, 0.8f );
	}
}
