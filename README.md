Argus
=====
> ***Argus Panoptes** (All-seeing; Ancient Greek: Ἄργος Πανόπτης) or **Argos** 
> (Ancient Greek: Ἄργος) is a many-eyed giant in Greek mythology. The figure is 
> known for having generated the saying "the eyes of Argus", as in to be 
> "followed by the eyes of Argus", or "trailed by" them, or "watched by" them, 
> etc.*
>> [Wikipedia, "Argus Panoptes"][1]

Argus is a specialized, site-targeted reverse image search application. It 
continually indexes images from various sites, recording their perceptual hashes
in a clustered database.

Once indexed, users can then search the data set for their works, seeing where 
and when they've been uploaded.

## Requirements
  * Elasticsearch
  * .NET 5

## Components
Argus is composed of a suite of programs, each serving a specific purpose.

### Argus.Coordinator
The coordinator serves as the main node of the cluster, accepting collected 
images and evenly spreading them out to workers. Once fingerprinted, the 
coordinator indexes the images in Elasticsearch.

### Argus.Worker
Worker instances pull collected images from the coordinator, fingerprinting and
returning them for indexing.

Workers may join or leave the cluster at any time.

### Argus.Collector.*
Collectors are specialized programs that know how to continually download images
from one specific service. Once downloaded, the collectors pass the images to
the coordinator for distribution to workers.


[1]: https://en.wikipedia.org/wiki/Argus_Panoptes
