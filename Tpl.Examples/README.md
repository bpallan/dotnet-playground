# TPL Dataflow Examples

## Summary
TPL data flows allow you to create a data processing pipeline for your application which can efficiently process large amounts of data.  I have found it ideal for "cloud sync" or other "import" type applications that read data from a source such as a database or s3 file, transform it, and then save it to an api or other data store. Buffering, transformation, batching and broadcasting to multiple consumers are all supported.  It supports both parallel and async execution to optimally process both CPU and I/O bound flows.  Use DataflowBlockOptions.BoundedCapacity to set the maximum buffer size.

## Data Flow Blocks
### 1. BufferBlock
Use a buffer block when you need to control the flow of data into the pipeline.  Reasons could be memory constraints or limitations on the amount of volume that downstream consumers can handle.  

[Example 1](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L43) : When you are operating synchronously and the buffer is full, then requests to post new records to the buffer will be rejected (returns false).  

[Example 2](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L71) : If you want to avoid losing records when operating synchronously with a full buffer, then you can keep retrying the post until it succeeds.  

[Example 3](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L102) : If you are operating async using SendAsync, then the Task will remain incomplete until the buffer has room and which point the await will return.  

