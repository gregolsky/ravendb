using System;

namespace Raven.Embedded
{
    public abstract class RavenServerLocator
    {
        public abstract string ServerPath { get; }

        public virtual string Command => ServerPath;

        public virtual string CommandArguments => string.Empty;
    }
}
