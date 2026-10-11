using System;

namespace GoldSrc;

partial class Client
{
	[ConVar( "g_ragdoll_maxcount" )]
	private static float RagdollMaxCount { get; set; } = 8f;

	[ConVar( "g_ragdoll_fadespeed" )]
	public static float RagdollFadeSpeed { get; set; } = 600f;

	[ConVar( "ragdoll_sleepaftertime" )]
	public static float RagdollSleepAfterTime { get; set; } = 5f;

	private const string BoltModel = "models/crossbow_bolt.mdl";
	private const float BoltLifeTime = 30f;
	private const float ExplosionLift = 32f;
	private const float ExplosionStrength = 500f;

	private readonly List<ClientRagdoll> _ragdolls = [];

	public void CreateRagdoll( int entityIndex, Vector3 force, int forceBone )
	{
		if ( GetEntity( entityIndex )?.CreateRagdoll( force, forceBone ) is { } ragdoll )
			_ragdolls.Add( ragdoll );
	}

	public void RagdollImpact( Vector3 start, Vector3 end )
	{
		var simulating = 0;

		foreach ( var ragdoll in _ragdolls )
		{
			if ( ragdoll.IsValid() && !ragdoll.Asleep )
				simulating++;
		}

		if ( simulating > 1 )
			return;

		foreach ( var hit in Scene.Trace.Ray( start, end ).WithTag( ClientRagdoll.Tag ).RunAll() )
		{
			if ( hit.GameObject.GetComponentInParent<ClientRagdoll>() is not { } ragdoll || ragdoll.CreateTime == ClientTime )
				continue;

			ragdoll.Impact( hit.HitPosition, end - start );

			return;
		}
	}

	public void RagdollExplosion( Vector3 origin, int radius, int magnitude )
	{
		if ( radius == 0 || magnitude == 0 )
			return;

		foreach ( var ragdoll in _ragdolls )
		{
			if ( !ragdoll.IsValid() || ragdoll.CreateTime == ClientTime )
				continue;

			var position = ragdoll.Origin;
			var direction = position - origin;
			var distance = MathLib.VectorNormalize( ref direction );
			var force = magnitude - ((float)magnitude / radius * distance);

			if ( force <= 1f )
				continue;

			_pmove.UseHull = 2;

			if ( _pmove.PlayerTrace( origin, position, TraceFlags.StudioBox, -1 ).Fraction < 1f )
				continue;

			ragdoll.Explode( ((direction * force) + new Vector3( 0f, 0f, ExplosionLift )) * ExplosionStrength );
		}
	}

	public void BoltImpact( Vector3 origin, Vector3 direction )
	{
		if ( string.Equals( TraceTexture( 0, origin, origin + (direction * 16f) ), "sky", StringComparison.OrdinalIgnoreCase ) )
			return;

		foreach ( var hit in Scene.Trace.Ray( origin, origin - (direction * 128f) ).WithTag( ClientRagdoll.Tag ).RunAll() )
		{
			if ( hit.GameObject.GetComponentInParent<ClientRagdoll>() is not { } ragdoll || hit.GameObject.GetComponent<Rigidbody>() is not { } body )
				continue;

			ragdoll.Pin( body, hit.HitPosition, origin );

			break;
		}

		if ( GetModelByIndex( FindModelIndex( BoltModel ) ) is not { } model || AllocTempEntity( origin - (direction * 8f), model ) is not { } bolt )
			return;

		bolt.Angles = MathLib.VectorAngles( direction );

		if ( _maxClients > 1 )
		{
			bolt.Die = (float)(ClientTime + BoltLifeTime);
			return;
		}

		bolt.Die = float.PositiveInfinity;
		bolt.Flags |= TempEntityFlags.Persist;
	}

	private void ClearRagdolls()
	{
		foreach ( var ragdoll in _ragdolls )
		{
			if ( !ragdoll.IsValid() )
				continue;

			ragdoll.GameObject.Enabled = false;
			ragdoll.GameObject.Destroy();
		}

		_ragdolls.Clear();
	}

	private void UpdateRagdolls()
	{
		var maxCount = (int)RagdollMaxCount;

		for ( var i = 0; i < _ragdolls.Count; i++ )
		{
			var ragdoll = _ragdolls[i];

			if ( !ragdoll.IsValid() )
			{
				_ragdolls.RemoveAt( i-- );
				continue;
			}

			if ( _ragdolls.Count <= maxCount || !CullBox( _frustum, ragdoll.Bounds.Mins, ragdoll.Bounds.Maxs ) )
				continue;

			ragdoll.FadeOut();

			_ragdolls.RemoveAt( i );

			return;
		}

		for ( var i = 0; i < _ragdolls.Count && _ragdolls.Count > maxCount; i++ )
		{
			if ( !_ragdolls[i].Asleep )
				continue;

			_ragdolls[i].FadeOut();

			_ragdolls.RemoveAt( i-- );
		}
	}
}
