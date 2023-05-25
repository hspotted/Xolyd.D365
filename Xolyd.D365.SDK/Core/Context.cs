using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Xolyd.D365.SDK.Core
{
  public class Context : ITracingService, IOrganizationService
  {
    private readonly Lazy<IOrganizationService> _lazyOrganizationService;
    private readonly ITracingService _tracingService;

    public Context(
      IServiceProvider serviceProvider,
      bool runAsASystem)
    {
      _tracingService = serviceProvider.GetService(typeof(ITracingService)) as ITracingService;
      PluginExecutionContext = serviceProvider.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext;

      var organizationServiceFactory =
        serviceProvider.GetService(typeof(IOrganizationServiceFactory)) as IOrganizationServiceFactory;

      _lazyOrganizationService = runAsASystem
        ? new Lazy<IOrganizationService>(() => organizationServiceFactory?.CreateOrganizationService(null))
        : new Lazy<IOrganizationService>(() =>
          organizationServiceFactory?.CreateOrganizationService(PluginExecutionContext.UserId));
    }

    public Context(
      IOrganizationService service,
      ITracingService tracingService)
    {
      _tracingService = tracingService;
      _lazyOrganizationService = new Lazy<IOrganizationService>(() => service);
    }

    public Entity GetFullEntity()
    {
      var result = new Entity(Target.LogicalName, Target.Id);
      result.Attributes.AddRange(Target.Attributes);

      if (PreImage != null)
      {
        result.Attributes.AddRange(PostImage.Attributes.Where(a => result.Attributes.Contains(a.Key)));
      }

      if (PostImage != null)
      {
        result.Attributes.AddRange(PostImage.Attributes.Where(a => !result.Attributes.Contains(a.Key)));
      }

      return result;
    }

    public void Trace(
      string format,
      params object[] arguments)
    {
      var message = string.Format(format, arguments);
      _tracingService.Trace(DateTime.UtcNow.ToString("T.fff") + message);
    }

    public Guid Save(Entity entity)
    {
      if (entity.Id.Equals(Guid.Empty))
      {
        return Service.Create(entity);
      }

      Service.Update(entity);
      return entity.Id;
    }

    [Obsolete("Use Save instead!")]
    public Guid Create(Entity entity)
    {
      Trace($"Creating {entity.LogicalName} with {entity.Attributes.Count} attributes");
      var result = Service.Create(entity);
      Trace("Created!");

      return result;
    }

    [Obsolete("Use Save instead!")]
    public void Update(Entity entity)
    {
      Trace($"Updating {entity.LogicalName} with {entity.Attributes.Count} attributes");
      Service.Update(entity);
      Trace("Updated!");
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
      Trace($"Retrieving {entityName} {id} with {columnSet.Columns.Count} attributes");
      var result = Service.Retrieve(entityName, id, columnSet);
      Trace("Retrieved!");

      return result;
    }

    public Entity Retrieve(EntityReference reference, ColumnSet columnSet)
      => Retrieve(reference.LogicalName, reference.Id, columnSet);

    public void Delete(string entityName, Guid id)
    {
      Trace($"Deleting {entityName} {id}");
      Service.Delete(entityName, id);
      Trace("Deleted!");
    }

    public void Delete(EntityReference reference)
      => Delete(reference.LogicalName, reference.Id);

    public OrganizationResponse Execute(OrganizationRequest request)
    {
      Trace($"Executing {request}");
      var result = Service.Execute(request);
      Trace($"Executed!");

      return result;
    }

    public void Associate(string entityName, Guid entityId, Relationship relationship,
      EntityReferenceCollection relatedEntities)
    {
      Trace(
        $"Associating {entityName} {entityId} over {relationship.SchemaName} with {relatedEntities.Count} {string.Join(", ", relatedEntities.Select((r => r.LogicalName)))}");
      Service.Associate(entityName, entityId, relationship, relatedEntities);
      Trace("Associated!");
    }

    public void Associate(EntityReference reference, Relationship relationship,
      EntityReferenceCollection relatedEntities)
      => Associate(reference.LogicalName, reference.Id, relationship, relatedEntities);

    public void Disassociate(string entityName, Guid entityId, Relationship relationship,
      EntityReferenceCollection relatedEntities)
    {
      Trace(
        $"Disassociating {entityName} {entityId} over {relationship.SchemaName} with {relatedEntities.Count} {string.Join(", ", relatedEntities.Select((r => r.LogicalName)))}");
      Service.Disassociate(entityName, entityId, relationship, relatedEntities);
      Trace("Disassociated!");
    }

    public void Disassociate(EntityReference reference, Relationship relationship,
      EntityReferenceCollection relatedEntities)
      => Disassociate(reference.LogicalName, reference.Id, relationship, relatedEntities);

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
      Trace($"Retrieving with {query}");
      var result = Service.RetrieveMultiple(query);
      Trace($"Retrieved {result.Entities.Count} {result.EntityName}");

      return result;
    }

    public IPluginExecutionContext PluginExecutionContext { get; }
    public IOrganizationService Service => _lazyOrganizationService.Value;

    public Entity Target => new Lazy<Entity>(() =>
      PluginExecutionContext.InputParameters.TryGetValue("Target", out Entity target) ? target : null).Value;

    public Entity PreImage => PluginExecutionContext.PreEntityImages.Select(i => i.Value).FirstOrDefault();
    public Entity PostImage => PluginExecutionContext.PostEntityImages.Select(i => i.Value).FirstOrDefault();
    public Entity FullEntity => GetFullEntity();
  }
}