using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using Xolyd.D365.SDK.Core;

namespace Xolyd.D365.SDK.Utilities
{
  public static class RelationshipExtensions
  {
    public static Entity GetParentEntity(
      this Entity entity,
      Context context,
      string lookupName,
      params string[] columns)
    {
      if (entity == null || !entity.TryGetAttributeValue(lookupName, out EntityReference parentReference))
      {
        return null;
      }

      return context.Retrieve(parentReference, new ColumnSet(columns));
    }

    public static IEnumerable<Entity> GetChildEntities(
      this Entity entity,
      Context context,
      string childrenName,
      string lookupName,
      FilterExpression filterExpression,
      params string[] columns)
    {
      if (entity == null)
      {
        return null;
      }

      var query = new QueryExpression(childrenName);
      query.ColumnSet.AddColumns(columns);
      query.Criteria.AddCondition(lookupName, ConditionOperator.Equal, entity.Id);
      query.Criteria.AddFilter(filterExpression);

      return context.RetrieveMultiple(query).Entities;
    }


    public static IEnumerable<Entity> GetChildEntities(
      this Entity entity,
      Context context,
      string childrenName,
      string lookupName,
      params string[] columns)
    {
      if (entity == null)
      {
        return null;
      }

      var query = new QueryExpression(childrenName);
      query.ColumnSet.AddColumns(columns);
      query.Criteria.AddCondition(lookupName, ConditionOperator.Equal, entity.Id);

      return context.RetrieveMultiple(query).Entities;
    }
  }
}