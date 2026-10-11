using GoldSrc;

namespace HalfLife;

public static partial class Combat
{
	private static BaseEntity _multiDamageEntity;
	private static float _multiDamageAmount;
	private static Vector3 _multiDamageForce;

	public static int MultiDamageType { get; set; }
	public static Vector3 DamageForce { get; set; }

	public static void ClearMultiDamage()
	{
		_multiDamageEntity = null;
		_multiDamageAmount = 0f;
		_multiDamageForce = Vector3.Zero;
		MultiDamageType = 0;
		DamageForce = Vector3.Zero;
	}

	public static void ApplyMultiDamage( BaseEntity inflictor, BaseEntity attacker )
	{
		if ( _multiDamageEntity is null )
			return;

		DamageForce = _multiDamageForce;

		_multiDamageEntity.TakeDamage( inflictor, attacker, _multiDamageAmount, MultiDamageType );

		DamageForce = Vector3.Zero;
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
			_multiDamageForce = Vector3.Zero;
		}

		_multiDamageAmount += damage;
		_multiDamageForce += DamageForce;

		DamageForce = Vector3.Zero;
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

	public static void RagdollImpact( Vector3 start, Vector3 end )
	{
		if ( GameCvars.Ragdolls == 0f )
			return;

		var server = Server.Current;

		server.MessageBegin( MessageDest.Pvs, UserMessages.RagdollImpact, end );
		server.WriteCoord( start.x );
		server.WriteCoord( start.y );
		server.WriteCoord( start.z );
		server.WriteCoord( end.x );
		server.WriteCoord( end.y );
		server.WriteCoord( end.z );
		server.MessageEnd();
	}

	public static void RagdollExplosion( Vector3 origin, int radius, int magnitude )
	{
		if ( GameCvars.Ragdolls == 0f )
			return;

		var server = Server.Current;

		server.MessageBegin( MessageDest.Pas, UserMessages.RagdollExplosion, origin );
		server.WriteCoord( origin.x );
		server.WriteCoord( origin.y );
		server.WriteCoord( origin.z );
		server.WriteShort( radius );
		server.WriteShort( magnitude );
		server.MessageEnd();
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
