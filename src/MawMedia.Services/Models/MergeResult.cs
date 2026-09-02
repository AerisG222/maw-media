namespace MawMedia.Services.Models;

// the outcome of media.merge_places.
//
// HadCover is what the repository needs to finish the job: the loser's row is gone
// but its published cover file is not, and the database cannot reach the
// filesystem.  a cover left behind would still be served to anyone holding the
// dead place's id.
class MergeResult
{
    public int Result { get; set; }
    public bool HadCover { get; set; }
    public int MovedLocations { get; set; }
    public int MovedChildren { get; set; }
}
