using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Server
{
	private int[] _nodeVisFrames;
	private int[] _leafVisFrames;
	private int _visFrameCount;

	private readonly Dictionary<int, byte[]> _pas = [];

	private byte[] _checkPvs;
	private byte[] _fatPvs;
	private int _lastCheck;
	private double _lastCheckTime;

	public Edict FindClientInPvs( Edict edict )
	{
		if ( Time - _lastCheckTime >= 0.1 )
		{
			_lastCheck = NewCheckClient( _lastCheck );
			_lastCheckTime = Time;
		}

		if ( _edicts[_lastCheck] is not { HasPrivateData: true } client )
			return _edicts[0];

		var leaf = WorldModel.PointInLeaf( edict.Origin + edict.ViewOffset ) - 1;

		if ( leaf >= 0 && (_checkPvs[leaf >> 3] & (1 << (leaf & 7))) != 0 )
			return client;

		return _edicts[0];
	}

	private int NewCheckClient( int check )
	{
		if ( check < 1 )
			check = 1;

		if ( check > MaxClients )
			check = MaxClients;

		var i = check == MaxClients ? 1 : check + 1;

		for ( ; ; i++ )
		{
			if ( i == MaxClients + 1 )
				i = 1;

			if ( i == check || (_edicts[i] is { HasPrivateData: true } candidate && (candidate.EntityFlags & EntityFlags.NoTarget) != EntityFlags.NoTarget) )
				break;
		}

		var client = _edicts[i];

		_checkPvs = WorldModel.LeafPvs( WorldModel.PointInLeaf( client.Origin + client.ViewOffset ) );

		return i;
	}

	public List<Edict> EntitiesInPvs( Edict edict )
	{
		var leaf = WorldModel.PointInLeaf( edict.Origin + edict.ViewOffset );
		var pvs = WorldModel.LeafPvs( leaf );

		WorldModel.MarkLeaves( pvs, _nodeVisFrames, _leafVisFrames, ++_visFrameCount );

		var chain = new List<Edict>();

		for ( var i = _numEdicts - 1; i >= 1; i-- )
		{
			var check = _edicts[i];
			if ( check is null )
				continue;

			var box = check.MoveType == MoveType.Follow && check.AimEntity is { } aim ? aim : check;

			if ( WorldModel.PvsNode( 0, box.AbsMin, box.AbsMax, _nodeVisFrames, _leafVisFrames, _visFrameCount ) )
				chain.Add( check );
		}

		return chain;
	}

	private void FindTouchedLeafs( Edict edict, int node, ref int topNode )
	{
		while ( true )
		{
			if ( node < 0 )
			{
				var leaf = -1 - node;

				if ( WorldModel.Leafs[leaf].Contents == Contents.Solid )
					return;

				if ( edict.NumLeafs >= Edict.MaxLeafs )
				{
					edict.NumLeafs = Edict.MaxLeafs + 1;
					return;
				}

				edict.LeafNums[edict.NumLeafs++] = (short)(leaf - 1);
				return;
			}

			var plane = WorldModel.Planes[WorldModel.Nodes[node].PlaneNum];
			int sides;

			if ( (int)plane.Type < 3 )
			{
				var axis = (int)plane.Type;

				if ( edict.AbsMin[axis] < plane.Dist )
					sides = plane.Dist < edict.AbsMax[axis] ? 3 : 2;
				else
					sides = 1;
			}
			else
			{
				sides = Bsp.File.BoxOnPlaneSide( edict.AbsMin, edict.AbsMax, plane );
			}

			if ( sides == 3 && topNode == -1 )
				topNode = node;

			if ( (sides & 1) != 0 )
				FindTouchedLeafs( edict, WorldModel.Nodes[node].Child0, ref topNode );

			if ( (sides & 2) == 0 )
				return;

			node = WorldModel.Nodes[node].Child1;
		}
	}

	public int CheckVisibility( Edict edict, byte[] set )
	{
		if ( set is null )
			return 1;

		if ( edict.HeadNode < 0 )
		{
			for ( var i = 0; i < edict.NumLeafs; i++ )
			{
				var leaf = edict.LeafNums[i];

				if ( (set[leaf >> 3] & (1 << (leaf & 7))) != 0 )
					return 1;
			}

			return 0;
		}

		for ( var i = 0; i < Edict.MaxLeafs; i++ )
		{
			var leaf = edict.LeafNums[i];

			if ( leaf == -1 )
				break;

			if ( (set[leaf >> 3] & (1 << (leaf & 7))) != 0 )
				return 1;
		}

		if ( !HeadNodeVisible( edict.HeadNode, set, out var visible ) )
			return 0;

		edict.LeafNums[edict.NumLeafs] = (short)visible;
		edict.NumLeafs = (edict.NumLeafs + 1) % Edict.MaxLeafs;

		return 2;
	}

	private bool HeadNodeVisible( int node, byte[] set, out int visible )
	{
		visible = -1;

		while ( node >= 0 )
		{
			if ( HeadNodeVisible( WorldModel.Nodes[node].Child0, set, out visible ) )
				return true;

			node = WorldModel.Nodes[node].Child1;
		}

		var leaf = -1 - node;

		if ( WorldModel.Leafs[leaf].Contents == Contents.Solid )
			return false;

		leaf--;

		if ( (set[leaf >> 3] & (1 << (leaf & 7))) == 0 )
			return false;

		visible = leaf;
		return true;
	}

	public byte[] SetFatPvs( Vector3 origin )
	{
		var bytes = (WorldModel.Models[0].VisLeafs + 31) >> 3;

		if ( _fatPvs is null || _fatPvs.Length != bytes )
			_fatPvs = new byte[bytes];
		else
			Array.Clear( _fatPvs );

		AddToFatPvs( origin, 0 );

		return _fatPvs;
	}

	private void AddToFatPvs( Vector3 origin, int node )
	{
		while ( node >= 0 )
		{
			var plane = WorldModel.Planes[WorldModel.Nodes[node].PlaneNum];
			var distance = (plane.Normal.y * origin.y) + (plane.Normal.x * origin.x) + (plane.Normal.z * origin.z) - plane.Dist;

			if ( distance > 8f )
			{
				node = WorldModel.Nodes[node].Child0;
				continue;
			}

			if ( distance >= -8f )
				AddToFatPvs( origin, WorldModel.Nodes[node].Child0 );

			node = WorldModel.Nodes[node].Child1;
		}

		var leaf = -1 - node;

		if ( WorldModel.Leafs[leaf].Contents == Contents.Solid )
			return;

		var pvs = WorldModel.LeafPvs( leaf );

		for ( var i = 0; i < pvs.Length; i++ )
			_fatPvs[i] |= pvs[i];
	}

	private void LoadVisibility()
	{
		_nodeVisFrames = new int[WorldModel.Nodes.Length];
		_leafVisFrames = new int[WorldModel.Leafs.Length];
		_visFrameCount = 0;

		_pas.Clear();
	}

	private byte[] LeafPas( int leaf )
	{
		if ( _pas.TryGetValue( leaf, out var cached ) )
			return cached;

		var leafCount = WorldModel.Models[0].VisLeafs + 1;
		var pvs = WorldModel.LeafPvs( leaf );
		var pas = pvs.AsSpan().ToArray();

		for ( var i = 0; i < pvs.Length * 8; i++ )
		{
			if ( (pvs[i >> 3] & (1 << (i & 7))) == 0 || i + 1 >= leafCount )
				continue;

			var other = WorldModel.LeafPvs( i + 1 );

			for ( var j = 0; j < pas.Length; j++ )
				pas[j] |= other[j];
		}

		return _pas[leaf] = pas;
	}
}
