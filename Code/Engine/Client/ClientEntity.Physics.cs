namespace GoldSrc;

partial class ClientEntity
{
	private ModelCollider _collider;
	private Model _colliderModel;

	private void UpdateCollision( Client client, Model model )
	{
		var solid = !ClientOwned && model is BrushModel && !client.HasServer && (Index == 0 || (Visible && CurState.Solid == (int)Solid.Bsp));

		if ( solid != Tags.Has( ScenePhysics.WorldTag ) )
			Tags.Set( ScenePhysics.WorldTag, solid );

		if ( solid && _collider is null )
		{
			_collider = AddComponent<ModelCollider>();
			_collider.Static = Index == 0;
		}

		if ( _collider is null )
			return;

		if ( solid && _colliderModel != model )
		{
			_colliderModel = model;
			_collider.Model = model.RenderModel;
		}

		_collider.Enabled = solid;
	}
}
