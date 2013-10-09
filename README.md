## Punchclock: A library for managing concurrent operations

Punchclock is the low-level scheduling and prioritization library used by
[Akavache](https://github.com/github/akavache) to orchestrate pending
concurrent operations.

### What even does that mean?

Ok, so you've got a shiny mobile phone app and you've got async/await.
Awesome! It's so easy to issue network requests, why not do it all the time?
After your users one-:star2: you for your app being slow, you discover that
you're issuing *way* too many requests at the same time. 

Then, you try to manage issuing less requests by hand, and it becomes a
spaghetti mess as different parts of your app reach into each other to try to
figure out who's doing what. Let's figure out a better way.

### So many words, gimme the examples

```cs
var wc = new WebClient();
var opQueue = new OperationQueue(2 /*at a time*/);

// Download a bunch of images
var foo = opQueue.Enqueue(1, 
    () => wc.DownloadFile("https://example.com/foo.jpg", "foo.jpg"));
var bar = opQueue.Enqueue(1, 
    () => wc.DownloadFile("https://example.com/bar.jpg", "bar.jpg"));
var baz = opQueue.Enqueue(1, 
    () => wc.DownloadFile("https://example.com/baz.jpg", "baz.jpg"));
var bamf = opQueue.Enqueue(1, 
    () => wc.DownloadFile("https://example.com/bamf.jpg", "bamf.jpg"));

// We'll be downloading the images two at a time, even though we started 
// them all at once
await Task.WaitAll(foo, bar, baz, bamf);
```

Now, in a completely different part of your app, if you need something right
away, you can specify it via the priority:

```cs
// This file is super important, we don't care if it cuts in line in front
// of some images or other stuff
var wc = new WebClient();
await opQueue.Enqueue(10 /* It's Important */, 
    () => wc.DownloadFileTaskAsync("http://example.com/cool.txt", "./cool.txt"));
```

## What else can this library do

* Cancellation via CancellationTokens or via Observables
* Ensure certain operations don't run concurrently via a key
* Queue pause / resume
