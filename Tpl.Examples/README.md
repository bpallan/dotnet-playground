# TPL Dataflow Examples

## Summary
TPL data flows allow you to create a data processing pipeline for your application which can efficiently process large amounts of data.  I have found it ideal for "cloud sync" or other "import" type applications that read data from a source such as a database or s3 file, transform it, and then save it to an api or other data store. Buffering, transformation, batching and broadcasting to multiple consumers are all supported.  It supports both parallel and async execution to optimally process both CPU and I/O bound flows.  

## Data Flow Blocks
### 1. BufferBlock
Use a buffer block when you need to control the flow of data into the pipeline.  Reasons could be memory constraints or limitations on the amount of volume that downstream consumers can handle.  Use DataflowBlockOptions.BoundedCapacity to set the maximum buffer size.

[Example 1](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L43) : When you are operating synchronously and the buffer is full, then requests to post new records to the buffer will be rejected (returns false).  

[Example 2](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L71) : If you want to avoid losing records when operating synchronously with a full buffer, then you can keep retrying the post until it succeeds.  

[Example 3](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L102) : If you are operating async using SendAsync, then the Task will remain incomplete until the buffer has room and which point the await will return.  

### 2. BroadcastBlock
Use a broadcast block when you have multiple consumers that all need to receive a copy of the message posted to the block. 

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L131) : Use a broadcast block to send messages from 1 producer to multiple consumers.  

### 3. ActionBlock
An action block is typically the end of the pipeline. It receives data but does not return anything.  Use ExecutionOptions.MaxDegreeOfParallelism to set the maximum number of threads that can execute concurrently.  You would typically set this to a number higher than the number of cores on the server to allow additional concurrency while waiting on i/o threads to complete.  

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L161) : Used by itself an action block functions very much like Parallel.ForEach loop with the caveat being that it supports async calls.  

### 4. TransformBlock
Unlike an action block, this supports both input and output.  Typically this block is used to transform the incomming data into a new form to be used by down stream blocks in the pipeline.  Like an ActionBlock, you can set the maximum concurrency via ExecutionOptions.MaxDegreeOfParallelism.

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L182) : Transform from import data type to the input type of the action block.

### 5. TransformManyBlock
This block accepts a single piece of input data and returns multiple pieces of output data.  This is a similar concept to SelectMany in LINQ.  

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L209) : Accept a single json string and return a list of deserialized objects for processing.

### 6. BatchBlock
This block accumulates messages until it reaches the defined theshold and then sends the messages on to the next step in the pipeline.  This is very useful for feeding data to an api or database in batches.  Complete will flush any remaining messages in the batch.  Not calling Complete can be risky in a low volume application as messages might sit int he block for a long tme before enough records are reached.  See TimedBatchBlock example below one possible solution to this issue.

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/BasicExamples.cs#L234) : Send messages in batches of 10.  

### 7. TimedBatchBlock
This is not an official data block type but something we derived to avoid messages getting stuck during low volume and to properly allow Rebus to handle exceptions that occur duing batch execution.
1. Batch will be flushed if no messages have been received for a (configured) time and threshold is not met.  IE. Timeout is reset each time a new message arrives.
1. Tasks will remain incomplete until batch is completed or timeout.  
1. Designed to be used as a static or singleton w/in the application and to be populated via many different threads.  A single threaded execution will cause every message to wait the full delay before being sent.

[Example](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/TimedBatchBlockExamples.cs#L34) : Fire several batches of records but allow the last batch to remain partially full and verify it is send after the timeout has expired.

## Linking Blocks Together
Link blocks togehter to form a pipline using LinkTo.  When executing a data flow w/in a class instance (blocks are not static/singleton), then you will want call Complete on the pipeline and wait for it to finish. This is made easier by passing LinkOptions.PropagateCompletion to true in your LinkTo statements.  That way you call Complete on your first block and it will propagate to the rest of the blocks.  You then await the last block to complete.  If you are using the pipeline as a singleton then you do not want to call Complete as that will stop the block/pipeline from receiving any more records.  Messages will start flowing through the pipeline as soon as you send them, you do not have to call Complete first.

## End to End Example
This example shows a few different ways that people typically send records to a service w/in a sync/import/etc application.  This example is based upon a real world cloud sync application that I worked on in which saving 100 records was only a few ms slower than saving a single record.  That can vary drastically depending on what all is involved in saving a record, but it is signicantly faster to save records in batch the vast majority of the time.  

[Save 1 at a time](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/EndToEndExamples.cs#L34) : Read the records returned from the import service and calls the customer service 1 record at a time using only a single thread.  Due to this being so slow we only process 1/1000 of the record as the last 2 examples.

[Save 1 at a time in parallel](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/EndToEndExamples.cs#L58) : This is the same as the above example but we use a Parallel.ForEach loop to process up to 10 save operations at a time.  Because Parallel.ForEach does not support async, we have to read the entire set of import data into memory at the beginning.  Due to being much slower, we only process 1/100 of the records as the last 2 examples.

[Save batches of 100 in parallel](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/EndToEndExamples.cs#L89) : This is basically hand-rolled batching using a Parallel.ForEach loop.  Like the previous example, we must read the entire import data set into memory.  Performance is very good so we can process the entire 100k records.  For this example, performance is almost as fast as using TPL even w/out having any async support.  It definitely doesn't code as nice and is more error prone though. 

[Save batches of 100 using TPL](https://github.com/bpallan/dotnet-playground/blob/master/Tpl.Examples/Tpl.Examples.Tests/EndToEndExamples.cs#L155) : Use TPL data flows to build a pipeline.  Batch -> Transform -> Action.  Both transform and action blocks are run with 10 degrees of parallelism.  This example has full async support so the entire import data set doesn't have to be read into memory at once.  For a larger import with slower I/O operations being performed, this would have likely excelled even more over the hand-rolled solution above.
