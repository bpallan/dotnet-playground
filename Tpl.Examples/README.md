# TPL Dataflow Examples

## Summary
TPL data flows allow you to create a data processing pipeline for your application which can efficiently process large amounts of data.  I have found it ideal for "cloud sync" or other "import" type applications that read data from a source such as a database or s3 file, transform it, and then save it to an api or other data store. Buffering, transformation, batching and broadcasting to multiple consumers are all supported.  It supports both parallel and async execution to optimally process both CPU and I/O bound flows.  
