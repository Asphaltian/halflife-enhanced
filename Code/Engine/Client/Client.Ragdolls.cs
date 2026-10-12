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

	private readonly List<ClientRagdoll> _ragdolls = [];
	private readonly ClientRagdoll[] _playerRagdolls = new ClientRagdoll[Server.MaxClientsLimit];

	public Vector3? LocalRagdollOrigin => _playerRagdolls[_playerNum] is { IsValid: true } ragdoll ? ragdoll.Origin : null;

	internal float Gravity => _moveVars.Gravity;

	public void CreateRagdoll( int entityIndex, Vector3 force, int forceBone )
	{
		if ( GetEntity( entityIndex )?.CreateRagdoll( force, forceBone ) is { } ragdoll )
			_ragdolls.Add( ragdoll );
	}

	public void CreatePlayerRagdoll( int entityIndex, Vector3 origin )
	{
		var player = entityIndex - 1;

		if ( player < 0 || player >= _maxClients )
			return;

		RemovePlayerRagdoll( entityIndex );

		var entity = CreateEntity( entityIndex );

		if ( player == _playerNum )
		{
			entity.SetLocalPlayerOrigin( CurrentFrame.PlayerState[player].Origin );
			entity.LinkPlayer( _messageTime );
			entity.Origin = _simOrigin;

			_playerRagdolls[player] = entity.CreateLocalPlayerRagdoll( player );
		}
		else if ( entity.Visible )
		{
			_playerRagdolls[player] = entity.CreateRemotePlayerRagdoll( player, (float)(ClientTime - ExInterp) );
		}
		else
		{
			_playerRagdolls[player] = ClientEntity.CreateDormantPlayerRagdoll( SetupPlayerModel( player, null ) as StudioModel, origin );
		}
	}

	public void RemovePlayerRagdoll( int entityIndex )
	{
		var player = entityIndex - 1;

		if ( player < 0 || player >= _playerRagdolls.Length )
			return;

		DestroyRagdoll( _playerRagdolls[player] );

		_playerRagdolls[player] = null;
	}

	private static void DestroyRagdoll( ClientRagdoll ragdoll )
	{
		if ( !ragdoll.IsValid() )
			return;

		ragdoll.GameObject.Enabled = false;
		ragdoll.GameObject.Destroy();
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
			RagdollExplosion( ragdoll, origin, radius, magnitude );

		foreach ( var ragdoll in _playerRagdolls )
			RagdollExplosion( ragdoll, origin, radius, magnitude );
	}

	private void RagdollExplosion( ClientRagdoll ragdoll, Vector3 origin, int radius, int magnitude )
	{
		if ( !ragdoll.IsValid() || ragdoll.CreateTime == ClientTime )
			return;

		var position = ragdoll.Origin;
		var direction = position - origin;
		var distance = MathLib.VectorNormalize( ref direction );
		var force = magnitude - ((float)magnitude / radius * distance);

		if ( force <= 1f )
			return;

		_pmove.UseHull = 2;

		if ( _pmove.PlayerTrace( origin, position, TraceFlags.StudioBox, -1 ).Fraction < 1f )
			return;

		ragdoll.Explode( (direction * force) + new Vector3( 0f, 0f, ExplosionLift ) );
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
			DestroyRagdoll( ragdoll );

		foreach ( var ragdoll in _playerRagdolls )
			DestroyRagdoll( ragdoll );

		_ragdolls.Clear();

		Array.Clear( _playerRagdolls );
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

			if ( _ragdolls.Count <= maxCount || (BoxInViewPvs( ragdoll.Bounds.Mins, ragdoll.Bounds.Maxs ) && !CullBox( _frustum, ragdoll.Bounds.Mins, ragdoll.Bounds.Maxs )) )
				continue;

			ragdoll.FadeOut();

			_ragdolls.RemoveAt( i );

			return;
		}

		while ( _ragdolls.Count > maxCount )
		{
			_ragdolls[0].FadeOut();

			_ragdolls.RemoveAt( 0 );
		}
	}

	internal float? WaterSurface( BBox bounds )
	{
		var point = bounds.Center.WithZ( bounds.Mins.z );

		if ( !InLiquid( point ) )
			return null;

		var bottom = bounds.Mins.z;
		var top = bounds.Maxs.z;

		if ( InLiquid( point.WithZ( top ) ) )
			return top;

		while ( top - bottom > 1f )
		{
			var middle = (bottom + top) * 0.5f;

			if ( InLiquid( point.WithZ( middle ) ) )
				bottom = middle;
			else
				top = middle;
		}

		return bottom;
	}

	private bool InLiquid( Vector3 point ) => (Bsp.Contents)PointContents( point ) is <= Bsp.Contents.Water and > Bsp.Contents.Translucent;

	public Vector3 TraceWorldBox( Vector3 start, Vector3 end, float extent )
	{
		var trace = Scene.Trace.Box( new BBox( new Vector3( -extent ), new Vector3( extent ) ), start, end ).WithTag( ScenePhysics.WorldTag ).Run();

		return trace.Hit ? trace.EndPosition : end;
	}
}
