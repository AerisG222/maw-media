namespace MawMedia.Services.Models;

// the outcome of media.set_place_cover / media.clear_place_cover.
//
// only a code.  the cover file is named for the place, so a caller that needs to
// remove one already knows what to remove - there is no displaced name for the
// database to hand back.
class CoverChange
{
    public int Result { get; set; }
}
