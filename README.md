# SimpleCrawler

A stupid and useless crawler written in C#.

## Usage

~~~sh
./SimpleCrawler <domain> \
    [-m <thread>] \
    [-d <depth>] \
    [-w] \
    [-f] \
    [-r <boolean>] \
    [-t <second>] \
    [-o <directory>]
~~~

### Simplest Usecase
~~~sh
./SimpleCrawler https://www.baidu.com/
~~~

### Setting Crawl Depth
Crawl depth defaults to 2.
~~~sh
./SimpleCrawler https://www.baidu.com/ -d 3
~~~

### Force Discover
The crawler tries to infer a file type from the HTTP `Content-Header` header, as well
as file extensions. Files identified as plain text files will be used to further
discover resources. The `--force-discover` switch will skip the inference step and
treat all files as plain text.
~~~sh
./SimpleCrawler https://www.baidu.com/ -f
~~~

### Write Simultaneously
Crawl result will be flushed to disk all at once after the entire process have finished.
The `--write-simul` switch will flush the file to disk once the resource has been fetched.
~~~sh
./SimpleCrawler https://www.baidu.com/ -w
~~~

### Verbose
Writing all debug information into stdout.
~~~sh
./SimpleCrawler https://www.baidu.com/ -v
~~~

### Output Path
Set the base output path, defaults to `crawl-result`.
~~~sh
./SimpleCrawler https://www.baidu.com/ -o ./output
~~~

### Timeout
Set the timeout for internal `HttpClientHandler` in seconds, defaults to `10`.
~~~sh
./SimpleCrawler https://www.baidu.com/ -t 100
~~~

### Do Not Follow Redirect
The crawler automatically follows redirect if status code is 301 or 302. You can disable redirect with `--follow-redirect false` switch.
~~~sh
./SimpleCrawler https://baidu.com/ -r false    # This won't crawl any data as request returns 302
~~~

### Log File
Specify where to write the log file with `--log`, defaults to `crawler.log`.
~~~sh
./SimpleCrawler https://www.baidu.com/ -l elsewhere.log
~~~

### Using Disk Cache
Skip cached file.

> [!NOTE]
>
> Due to limitations of how file paths are inferred, not all cached files are skipped.

~~~sh
./SimpleCrawler https://www.baidu.com/ -c
~~~


### Threading
Sets the thread count in thread pool.

> [!IMPORTANT]
> 
> This may not work as expected, use with caution.

~~~sh
./SimpleCrawler https://baidu.com/ -m 4
~~~

### Get Help
Get help with `--help`
~~~sh
./SimpleCrawler --help
~~~

## Performance

Nope. There's nothing here.

Come on, this crawler gives you 1000+ errors when crawling 2000 resources, why are you
expecting any performance data?