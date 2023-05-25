using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Xolyd.D365.SDK.Core
{
  public static class CanaryTracer
  {
    /// <summary>
    /// Default settings for the TraceContext
    /// </summary>
    /// <param name="tracingService">The tracer to trace the trace.</param>
    /// <param name="context">The plugin or workflow Context to trace.</param>
    public static void TraceContext(this ITracingService tracingService, IExecutionContext context) =>
      tracingService.TraceContext(context, false, true, false, false, false, null);

    /// <summary>
    /// Dumps everything interesting from the plugin Context to the plugin trace log
    /// </summary>
    /// <param name="tracingService">The tracer to trace the trace.</param>
    /// <param name="context">The plugin or workflow Context to trace.</param>
    /// <param name="parentContext">Set to true if any parent contexts shall be traced too.</param>
    /// <param name="attributeTypes">Set to true to include information about attribute types.</param>
    /// <param name="convertQueries">Set to true if any QueryExpression queries shall be converted to FetchXML and traced. Requires parameter Service to be set.</param>
    /// <param name="expandCollections">Set to true if EntityCollection objects should list all contained Entity objects with all fields available.</param>
    /// <param name="includeStage30">Set to true to also include plugins in internal stage.</param>
    /// <param name="service">Service used if convertQueries is true, may be null if not used.</param>
    public static void TraceContext(this ITracingService tracingService, IExecutionContext context, bool parentContext,
      bool attributeTypes, bool convertQueries, bool expandCollections, bool includeStage30,
      IOrganizationService service)
    {
      try
      {
        tracingService.TraceContext(context, parentContext, attributeTypes, convertQueries, expandCollections,
          includeStage30, service, 1);
      }
      catch (Exception ex)
      {
        tracingService.Trace("--- Exception while trying to TraceContext ---");
        tracingService.Trace($"Message : {ex.Message}");
      }
    }

    private static void TraceContext(this ITracingService tracingService, IExecutionContext context, bool parentContext,
      bool attributeTypes, bool convertQueries, bool expandCollections, bool includeStage30,
      IOrganizationService service, int depth)
    {
      var pluginContext = context as IPluginExecutionContext;
      if (includeStage30 || pluginContext?.Stage != 30)
      {
        tracingService.Trace("--- Context {0} Trace Start ---", depth);
        tracingService.Trace("Message : {0}", context.MessageName);
        if (pluginContext != null)
        {
          tracingService.Trace("Stage   : {0}", pluginContext.Stage);
        }

        tracingService.Trace("Mode    : {0}", context.Mode);
        tracingService.Trace("Depth   : {0}", context.Depth);
        tracingService.Trace("Entity  : {0}", context.PrimaryEntityName);
        if (!context.PrimaryEntityId.Equals(Guid.Empty))
        {
          tracingService.Trace("Id      : {0}", context.PrimaryEntityId);
        }

        tracingService.Trace("");

        tracingService.TraceAndAlign("InputParameters", context.InputParameters, attributeTypes, convertQueries,
          expandCollections, service);
        tracingService.TraceAndAlign("OutputParameters", context.OutputParameters, attributeTypes, convertQueries,
          expandCollections, service);
        tracingService.TraceAndAlign("SharedVariables", context.SharedVariables, attributeTypes, convertQueries,
          expandCollections, service);
        tracingService.TraceAndAlign("PreEntityImages", context.PreEntityImages, attributeTypes, convertQueries,
          expandCollections, service);
        tracingService.TraceAndAlign("PostEntityImages", context.PostEntityImages, attributeTypes, convertQueries,
          expandCollections, service);
        tracingService.Trace("--- Context {0} Trace End ---", depth);
      }

      if (parentContext && pluginContext?.ParentContext != null)
      {
        tracingService.TraceContext(pluginContext.ParentContext, parentContext, attributeTypes, convertQueries,
          expandCollections, includeStage30, service, depth + 1);
      }

      tracingService.Trace("");
    }

    private static void TraceAndAlign<T>(this ITracingService tracingService, string topic,
      IEnumerable<KeyValuePair<string, T>> parameterCollection, bool attributeTypes, bool convertQueries,
      bool expandCollections, IOrganizationService service)
    {
      if (parameterCollection == null || !parameterCollection.Any())
      {
        return;
      }

      tracingService.Trace(topic);
      var keyLen = parameterCollection.Max(p => p.Key.Length);
      foreach (var parameter in parameterCollection)
      {
        tracingService.Trace(
          $"  {parameter.Key}{new string(' ', keyLen - parameter.Key.Length)} = {ValueToString(parameter.Value, attributeTypes, convertQueries, expandCollections, service, 2)}");
      }
    }

    public static string ValueToString(object value, bool attributeTypes, bool convertQueries, bool expandCollections,
      IOrganizationService service, int indent = 1)
    {
      var indentString = new string(' ', indent * 2);
      switch (value)
      {
        case null:
          return $"{indentString}<null>";
        case EntityCollection collection:
        {
          var result =
            $"{collection.EntityName} collection\n  Records: {collection.Entities.Count}\n  TotalRecordCount: {collection.TotalRecordCount}\n  MoreRecords: {collection.MoreRecords}\n  PagingCookie: {collection.PagingCookie}";
          if (expandCollections && collection.Entities.Count > 0)
          {
            result += "\n" + ValueToString(collection.Entities, attributeTypes, convertQueries, expandCollections,
              service, indent + 1);
          }

          return result;
        }
        case IEnumerable<Entity> entities:
          return expandCollections
            ? $"{indentString}{string.Join($"\n{indentString}", entities.Select(e => ValueToString(e, attributeTypes, convertQueries, expandCollections, service, indent + 1)))}"
            : string.Empty;
        case Entity entity:
        {
          var keyLen = entity.Attributes.Count > 0 ? entity.Attributes.Max(p => p.Key.Length) : 50;
          return $"{entity.LogicalName} {entity.Id}\n{indentString}" + string.Join($"\n{indentString}",
            entity.Attributes.OrderBy(a => a.Key).Select(a =>
              $"{a.Key}{new string(' ', keyLen - a.Key.Length)} = {ValueToString(a.Value, attributeTypes, convertQueries, expandCollections, service, indent + 1)}"));
        }
        case ColumnSet columnSet:
        {
          var columnList = new List<string>(columnSet.Columns);
          columnList.Sort();
          return $"\n{indentString}" + string.Join($"\n{indentString}", columnList);
        }
        case FetchExpression fetchExpression:
          return $"{value}\n{indentString}{fetchExpression.Query}";
        case QueryExpression queryExpression when convertQueries && service != null:
        {
          var fetchXml = ((QueryExpressionToFetchXmlResponse)service.Execute(new QueryExpressionToFetchXmlRequest
            { Query = queryExpression })).FetchXml;
          return $"{queryExpression}\n{indentString}{fetchXml}";
        }
        default:
        {
          string result;

          switch (value)
          {
            case EntityReference entityReference:
              result = $"{entityReference.LogicalName} {entityReference.Id} {entityReference.Name}";
              break;
            case OptionSetValue optionSetValue:
              result = optionSetValue.Value.ToString();
              break;
            case Money money:
              result = money.Value.ToString(CultureInfo.CurrentCulture);
              break;
            default:
              result = value.ToString().Replace("\n", $"\n  {indentString}");
              break;
          }

          return result + (attributeTypes ? $" \t({value.GetType()})" : "");
        }
      }
    }

    public static void Write(this ITracingService tracer, string text)
    {
      tracer.Trace(DateTime.Now.ToString("HH:mm:ss.fff  ") + text);
    }
  }
}