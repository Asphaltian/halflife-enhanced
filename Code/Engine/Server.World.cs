using System;
namespace GoldSrc;

partial class Server
{
	private const int AreaDepth = 4;

	internal sealed class AreaNode
	{
		public int Axis;
		public float Dist;
		public AreaNode[] Children = new AreaNode[2];
		public LinkedList<Edict> TriggerEdicts = [];
		public LinkedList<Edict> SolidEdicts = [];
	}

	private AreaNode _areaNodes;

	public void LinkEdict( Edict edict, bool touchTriggers )
	{
		UnlinkEdict( edict );

		if ( edict.Index == 0 || !edict.IsValid )
			return;

		edict.SetAbsBox();

		if ( edict.MoveType == MoveType.Follow && edict.AimEntity is { } aim )
		{
			edict.HeadNode = aim.HeadNode;
			edict.NumLeafs = aim.NumLeafs;
			Array.Copy( aim.LeafNums, edict.LeafNums, Edict.MaxLeafs );
		}
		else
		{
			edict.NumLeafs = 0;
			edict.HeadNode = -1;

			var topNode = -1;

			if ( edict.ModelIndex != 0 )
				FindTouchedLeafs( edict, 0, ref topNode );

			if ( edict.NumLeafs > Edict.MaxLeafs )
			{
				edict.HeadNode = topNode;
				edict.NumLeafs = 0;
				Array.Fill( edict.LeafNums, (short)-1 );
			}
		}

		if ( edict.Solid == Solid.Not && edict.Skin >= -1 )
			return;

		if ( edict.Solid == Solid.Bsp && GetModel( edict.ModelIndex ) is null && string.IsNullOrEmpty( edict.ModelName ) )
		{
			Log.Info( $"Inserted {edict.ClassName} with no model" );
			return;
		}

		var node = _areaNodes;

		while ( node.Axis != -1 )
		{
			if ( edict.AbsMin[node.Axis] > node.Dist )
				node = node.Children[0];
			else if ( edict.AbsMax[node.Axis] < node.Dist )
				node = node.Children[1];
			else
				break;
		}

		var list = edict.Solid == Solid.Trigger ? node.TriggerEdicts : node.SolidEdicts;
		edict.AreaLink = list.AddLast( edict );

		if ( !touchTriggers || _touchLinksSemaphore )
			return;

		_touchLinksSemaphore = true;
		TouchLinks( edict, _areaNodes );
		_touchLinksSemaphore = false;
	}

	public void UnlinkEdict( Edict edict )
	{
		if ( edict.AreaLink is null )
			return;

		edict.AreaLink.List.Remove( edict.AreaLink );
		edict.AreaLink = null;
	}

	private void ClearWorld()
	{
		_areaNodes = CreateAreaNode( 0, _models[1].Mins, _models[1].Maxs );
	}

	private static AreaNode CreateAreaNode( int depth, Vector3 mins, Vector3 maxs )
	{
		var node = new AreaNode();

		if ( depth == AreaDepth )
		{
			node.Axis = -1;
			return node;
		}

		node.Axis = maxs.x - mins.x > maxs.y - mins.y ? 0 : 1;
		node.Dist = 0.5f * (maxs[node.Axis] + mins[node.Axis]);

		var mins2 = mins;
		var maxs1 = maxs;
		mins2[node.Axis] = node.Dist;
		maxs1[node.Axis] = node.Dist;

		node.Children[0] = CreateAreaNode( depth + 1, mins2, maxs );
		node.Children[1] = CreateAreaNode( depth + 1, mins, maxs1 );

		return node;
	}
}
