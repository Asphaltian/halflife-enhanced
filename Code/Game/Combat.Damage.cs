using GoldSrc;

namespace HalfLife;

public static partial class Combat
{
	private static BaseEntity _multiDamageEntity;
	private static float _multiDamageAmount;

	public static int MultiDamageType { get; set; }

	public static void ClearMultiDamage()
	{
		_multiDamageEntity = null;
		_multiDamageAmount = 0f;
		MultiDamageType = 0;
	}

	public static void ApplyMultiDamage( BaseEntity inflictor, BaseEntity attacker )
	{
		_multiDamageEntity?.TakeDamage( inflictor, attacker, _multiDamageAmount, MultiDamageType );
	}

	public static void AddMultiDamage( BaseEntity inflictor, BaseEntity entity, float damage, int damageType )
	{
		if ( entity is null )
			return;

		MultiDamageType |= damageType;

		if ( entity != _multiDamageEntity )
		{
			ApplyMultiDamage( inflictor, inflictor );

			_multiDamageEntity = entity;
			_multiDamageAmount = 0f;
		}

		_multiDamageAmount += damage;
	}

	public static void SpawnBlood( Vector3 spot, int bloodColor, float damage )
	{
		Util.BloodDrips( spot, BaseEntity.AttackDir, bloodColor, (int)damage );
	}

	public static int DamageDecal( BaseEntity entity, int damageType )
	{
		if ( entity is null )
			return Decals.Gunshot1 + Server.RandomLong( 0, 4 );

		return entity.DamageDecal( damageType );
	}

	public static void DecalGunshot( in Trace trace, Bullet bulletType )
	{
		if ( !Util.IsValidEntity( trace.Ent ) )
			return;

		if ( trace.Ent.Solid != Solid.Bsp && trace.Ent.MoveType != MoveType.PushStep )
			return;

		var entity = Util.IsNullEnt( trace.Ent ) ? null : trace.Ent as BaseEntity;

		if ( bulletType == Bullet.PlayerCrowbar )
			Util.DecalTrace( trace, DamageDecal( entity, BaseEntity.DamageClub ) );
		else
			Util.GunshotDecalTrace( trace, DamageDecal( entity, BaseEntity.DamageBullet ) );
	}

	public static void EjectBrass( Vector3 origin, Vector3 velocity, float rotation, int model, int soundType )
	{
		var server = Server.Current;

		server.MessageBegin( MessageDest.Pvs, Server.TempEntityMessage, origin );
		server.WriteByte( TempEntity.Model );
		server.WriteCoord( origin.x );
		server.WriteCoord( origin.y );
		server.WriteCoord( origin.z );
		server.WriteCoord( velocity.x );
		server.WriteCoord( velocity.y );
		server.WriteCoord( velocity.z );
		server.WriteAngle( rotation );
		server.WriteShort( model );
		server.WriteByte( soundType );
		server.WriteByte( 25 );
		server.MessageEnd();
	}
}
