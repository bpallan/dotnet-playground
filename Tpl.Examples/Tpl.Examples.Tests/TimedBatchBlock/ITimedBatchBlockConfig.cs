using System;
using System.Collections.Generic;
using System.Text;

namespace Tpl.Examples.Tests.TimedBatchBlock
{
    internal interface ITimedBatchBlockConfig
    {
        /// <summary>
        /// Numbers of messages required to trigger batch action.
        /// </summary>
        int BatchSize { get; }

        /// <summary>
        /// Batch timeout in milliseconds.
        /// </summary>
        int BatchTimeoutMs { get; }

        /// <summary>
        /// Thread timeout in milliseconds.
        /// Should be greater than BatchSize x BatchTimeout or might timeout before batch is triggered
        /// </summary>
        int ThreadTimeoutMs { get; }
    }
}
