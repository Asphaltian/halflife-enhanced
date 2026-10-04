using GoldSrc;

namespace HalfLife;

partial class BaseEntity
{
	private static int _tracerCount;

	public static Vector3 AttackDir { get; set; }

	public virtual int Classify() => ClassNone;

	public virtual void DeathNotice( BaseEntity child )
	{
	}

	public virtual int BloodColor => DontBleed;

	public virtual bool IsBspModel => Solid == Solid.Bsp || MoveType == MoveType.PushStep;

	public virtual bool ReflectGauss => IsBspModel && DamageMode == DamageNo;

	public virtual bool IsNetClient => false;

	public virtual string TeamId => "";

	public virtual bool IsMoving => !MathLib.VectorCompare( Velocity, Vector3.Zero );

	public virtual ToggleState GetToggleState() => ToggleState.AtTop;

	public virtual bool IsLockedByMaster => false;

	public virtual bool IsSneaking => false;

	public virtual Vector3 Center => (AbsMax + AbsMin) * 0.5f;

	public virtual Vector3 EyePosition => Origin + ViewOffset;

	public virtual Vector3 EarPosition => Origin + ViewOffset;

	public virtual Vector3 BodyTarget( Vector3 source ) => Center;

	public virtual void StartSneaking()
	{
	}

	public virtual void StopSneaking()
	{
	}

	public virtual void AddPoints( int score, bool allowNegativeScore )
	{
	}

	public virtual void AddPointsToTeam( int score, bool allowNegativeScore )
	{
	}

	public virtual int GiveAmmo( int amount, string name, int max ) => -1;

	public virtual BaseEntity Respawn() => null;

	public virtual bool BecomeProne() => false;

	public virtual bool IsInWorld
	{
		get
		{
			if ( Origin.x >= 4096f || Origin.y >= 4096f || Origin.z >= 4096f )
				return false;

			if ( Origin.x <= -4096f || Origin.y <= -4096f || Origin.z <= -4096f )
				return false;

			if ( Velocity.x >= 2000f || Velocity.y >= 2000f || Velocity.z >= 2000f )
				return false;

			if ( Velocity.x <= -2000f || Velocity.y <= -2000f || Velocity.z <= -2000f )
				return false;

			return true;
		}
	}

	public virtual int DamageDecal( int damageType )
	{
		if ( RenderMode == RenderMode.TransAlpha )
			return -1;

		if ( RenderMode != RenderMode.Normal )
			return Decals.BulletProof1;

		return Decals.Gunshot1 + Server.RandomLong( 0, 4 );
	}

	public virtual bool IsVisible( BaseEntity entity )
	{
		if ( (entity.EntityFlags & EntityFlags.NoTarget) == EntityFlags.NoTarget )
			return false;

		if ( (WaterLevel != 3 && entity.WaterLevel == 3) || (WaterLevel == 3 && entity.WaterLevel == 0) )
			return false;

		var trace = Util.TraceLine( Origin + ViewOffset, entity.EyePosition, IgnoreMonsters | IgnoreGlass, this );

		return trace.Fraction == 1f;
	}

	public virtual bool IsVisible( Vector3 origin )
	{
		var trace = Util.TraceLine( EyePosition, origin, IgnoreMonsters | IgnoreGlass, this );

		return trace.Fraction == 1f;
	}

	public virtual void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		var origin = trace.EndPos - (direction * 4f);

		if ( DamageMode == DamageNo )
			return;

		Combat.AddMultiDamage( attacker, this, damage, damageType );

		var blood = BloodColor;

		if ( blood != DontBleed )
		{
			Combat.SpawnBlood( origin, blood, damage );
			TraceBleed( damage, direction, trace, damageType );
		}
	}

	public virtual void TraceBleed( float damage, Vector3 direction, in Trace trace, int damageType )
	{
		if ( BloodColor == DontBleed )
			return;

		if ( damage == 0f )
			return;

		if ( (damageType & (DamageCrush | DamageBullet | DamageSlash | DamageBlast | DamageClub | DamageMortar)) == 0 )
			return;

		float noise;
		int count;

		if ( damage < 10f )
		{
			noise = 0.1f;
			count = 1;
		}
		else if ( damage < 25f )
		{
			noise = 0.2f;
			count = 2;
		}
		else
		{
			noise = 0.3f;
			count = 4;
		}

		for ( var i = 0; i < count; i++ )
		{
			var traceDirection = direction * -1f;

			traceDirection.x += Server.RandomFloat( -noise, noise );
			traceDirection.y += Server.RandomFloat( -noise, noise );
			traceDirection.z += Server.RandomFloat( -noise, noise );

			var bloodTrace = Util.TraceLine( trace.EndPos, trace.EndPos + (traceDirection * -172f), IgnoreMonsters, this );

			if ( bloodTrace.Fraction != 1f )
				Util.BloodDecalTrace( bloodTrace, BloodColor );
		}
	}

	public void FireBullets( uint shots, Vector3 source, Vector3 shootDirection, Vector3 spread, float distance, Bullet bulletType, int tracerFrequency = 4, int damage = 0, BaseEntity attacker = null )
	{
		var right = Server.Globals.Right;
		var up = Server.Globals.Up;

		attacker ??= this;

		Combat.ClearMultiDamage();
		Combat.MultiDamageType = DamageBullet | DamageNeverGib;

		for ( uint shot = 1; shot <= shots; shot++ )
		{
			float x, y, z;

			do
			{
				x = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
				y = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
				z = (x * x) + (y * y);
			}
			while ( z > 1f );

			var direction = shootDirection + (x * spread.x * right) + (y * spread.y * up);
			var end = source + (direction * distance);
			var trace = Util.TraceLine( source, end, DontIgnoreMonsters, this );
			var tracer = false;

			if ( tracerFrequency != 0 && (_tracerCount++ % tracerFrequency) == 0 )
			{
				var tracerSource = IsPlayer
					? source + new Vector3( 0f, 0f, -4f ) + (Server.Globals.Right * 2f) + (Server.Globals.Forward * 16f)
					: source;

				if ( tracerFrequency != 1 )
					tracer = true;

				Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, tracerSource );
				Server.WriteByte( TempEntity.Tracer );
				Server.WriteCoord( tracerSource.x );
				Server.WriteCoord( tracerSource.y );
				Server.WriteCoord( tracerSource.z );
				Server.WriteCoord( trace.EndPos.x );
				Server.WriteCoord( trace.EndPos.y );
				Server.WriteCoord( trace.EndPos.z );
				Server.MessageEnd();
			}

			if ( trace.Fraction != 1f )
			{
				ScenePhysicsDamage.BulletImpact( trace, bulletType, direction );

				var entity = (BaseEntity)trace.Ent;

				if ( damage != 0 )
				{
					entity.TraceAttack( attacker, damage, direction, trace, DamageBullet | (damage > 16 ? DamageAlwaysGib : DamageNeverGib) );

					TextureType.PlaySound( trace, source, end, bulletType );
					Combat.DecalGunshot( trace, bulletType );
				}
				else
				{
					switch ( bulletType )
					{
						default:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.MonDmg9MM, direction, trace, DamageBullet );

							TextureType.PlaySound( trace, source, end, bulletType );
							Combat.DecalGunshot( trace, bulletType );
							break;

						case Bullet.MonsterMp5:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.MonDmgMP5, direction, trace, DamageBullet );

							TextureType.PlaySound( trace, source, end, bulletType );
							Combat.DecalGunshot( trace, bulletType );
							break;

						case Bullet.Monster12mm:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.MonDmg12MM, direction, trace, DamageBullet );

							if ( !tracer )
							{
								TextureType.PlaySound( trace, source, end, bulletType );
								Combat.DecalGunshot( trace, bulletType );
							}

							break;

						case Bullet.None:
							entity.TraceAttack( attacker, 50f, direction, trace, DamageClub );

							TextureType.PlaySound( trace, source, end, bulletType );

							if ( !Util.IsNullEnt( trace.Ent ) && trace.Ent.RenderMode != RenderMode.Normal )
								Util.DecalTrace( trace, Decals.GlassBreak1 + Server.RandomLong( 0, 2 ) );

							break;
					}
				}
			}

			Util.BubbleTrail( source, trace.EndPos, (int)(distance * trace.Fraction / 64.0) );
		}

		Combat.ApplyMultiDamage( this, attacker );
	}

	private Vector3 ClientFireBulletsPlayer( uint shots, Vector3 spread, BaseEntity attacker, int sharedRandom )
	{
		var x = 0f;
		var y = 0f;

		for ( uint shot = 1; shot <= shots; shot++ )
		{
			if ( attacker is null )
			{
				float z;

				do
				{
					x = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
					y = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
					z = (x * x) + (y * y);
				}
				while ( z > 1f );
			}
			else
			{
				x = Util.SharedRandomFloat( (uint)sharedRandom + shot, -0.5f, 0.5f ) + Util.SharedRandomFloat( (uint)sharedRandom + 1 + shot, -0.5f, 0.5f );
				y = Util.SharedRandomFloat( (uint)sharedRandom + 2 + shot, -0.5f, 0.5f ) + Util.SharedRandomFloat( (uint)sharedRandom + 3 + shot, -0.5f, 0.5f );
			}
		}

		return new Vector3( x * spread.x, y * spread.y, 0f );
	}

	public Vector3 FireBulletsPlayer( uint shots, Vector3 source, Vector3 shootDirection, Vector3 spread, float distance, Bullet bulletType, int tracerFrequency = 4, int damage = 0, BaseEntity attacker = null, int sharedRandom = 0 )
	{
		if ( ClientSide )
			return ClientFireBulletsPlayer( shots, spread, attacker, sharedRandom );

		var right = Server.Globals.Right;
		var up = Server.Globals.Up;
		var x = 0f;
		var y = 0f;

		attacker ??= this;

		Combat.ClearMultiDamage();
		Combat.MultiDamageType = DamageBullet | DamageNeverGib;

		for ( uint shot = 1; shot <= shots; shot++ )
		{
			x = Util.SharedRandomFloat( (uint)sharedRandom + shot, -0.5f, 0.5f ) + Util.SharedRandomFloat( (uint)sharedRandom + 1 + shot, -0.5f, 0.5f );
			y = Util.SharedRandomFloat( (uint)sharedRandom + 2 + shot, -0.5f, 0.5f ) + Util.SharedRandomFloat( (uint)sharedRandom + 3 + shot, -0.5f, 0.5f );

			var direction = shootDirection + (x * spread.x * right) + (y * spread.y * up);
			var end = source + (direction * distance);
			var trace = Util.TraceLine( source, end, DontIgnoreMonsters, this );

			if ( trace.Fraction != 1f )
			{
				ScenePhysicsDamage.BulletImpact( trace, bulletType, direction );

				var entity = (BaseEntity)trace.Ent;

				if ( damage != 0 )
				{
					entity.TraceAttack( attacker, damage, direction, trace, DamageBullet | (damage > 16 ? DamageAlwaysGib : DamageNeverGib) );

					TextureType.PlaySound( trace, source, end, bulletType );
					Combat.DecalGunshot( trace, bulletType );
				}
				else
				{
					switch ( bulletType )
					{
						default:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.PlrDmg9MM, direction, trace, DamageBullet );
							break;

						case Bullet.PlayerMp5:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.PlrDmgMP5, direction, trace, DamageBullet );
							break;

						case Bullet.PlayerBuckshot:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.PlrDmgBuckshot, direction, trace, DamageBullet );
							break;

						case Bullet.Player357:
							entity.TraceAttack( attacker, HalfLifeDll.SkillData.PlrDmg357, direction, trace, DamageBullet );
							break;

						case Bullet.None:
							entity.TraceAttack( attacker, 50f, direction, trace, DamageClub );

							TextureType.PlaySound( trace, source, end, bulletType );

							if ( !Util.IsNullEnt( trace.Ent ) && trace.Ent.RenderMode != RenderMode.Normal )
								Util.DecalTrace( trace, Decals.GlassBreak1 + Server.RandomLong( 0, 2 ) );

							break;
					}
				}
			}

			Util.BubbleTrail( source, trace.EndPos, (int)(distance * trace.Fraction / 64.0) );
		}

		Combat.ApplyMultiDamage( this, attacker );

		return new Vector3( x * spread.x, y * spread.y, 0f );
	}

	public void SubStartFadeOut()
	{
		if ( RenderMode == RenderMode.Normal )
		{
			RenderAmt = 255f;
			RenderMode = RenderMode.TransTexture;
		}

		Solid = Solid.Not;
		AVelocity = Vector3.Zero;

		NextThink = (float)(Server.Globals.Time + 0.1);
		SetThink( SubFadeOut );
	}

	public void SubFadeOut()
	{
		if ( RenderAmt > 7f )
		{
			RenderAmt -= 7f;
			NextThink = (float)(Server.Globals.Time + 0.1);
		}
		else
		{
			RenderAmt = 0f;
			NextThink = (float)(Server.Globals.Time + 0.2);
			SetThink( SubRemove );
		}
	}
}
