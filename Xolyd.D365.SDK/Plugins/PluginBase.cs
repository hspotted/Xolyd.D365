using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Xolyd.D365.SDK.Core;

namespace Xolyd.D365.SDK.Plugins
{
  internal abstract class PluginBase : IPlugin
  {
    public void Execute(IServiceProvider serviceProvider)
    {
      var context = new Context(serviceProvider, RunAsASystem);
      context.TraceContext(context.PluginExecutionContext);

      if (!string.IsNullOrWhiteSpace(ExpectedEntity) &&
          ExpectedEntity != context.PluginExecutionContext.PrimaryEntityName)
      {
        context.Trace($"Wrong entity: {context.PluginExecutionContext.PrimaryEntityName}");
        return;
      }

      if (ExpectedMessages?.Length > 0 && !ExpectedMessages.Contains(context.PluginExecutionContext.MessageName))
      {
        context.Trace($"Wrong message: {context.PluginExecutionContext.MessageName}");
        return;
      }

      Execute(context);
    }

    public abstract void Execute(Context context);

    public abstract string ExpectedEntity { get; }
    public abstract string[] ExpectedMessages { get; }
    public abstract bool RunAsASystem { get; }
  }
}