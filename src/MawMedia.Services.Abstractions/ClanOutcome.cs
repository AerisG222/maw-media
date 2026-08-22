namespace MawMedia.Services.Abstractions;

// mirrors the integers the media.*_clan functions return.  an enum rather than a
// bool because the three failures need different http answers, and a caller that
// only cares whether it worked can still compare against Applied.
public enum ClanOutcome
{
    Applied = 0,

    // the clan does not exist, or belongs to someone else.  the two are
    // deliberately indistinguishable - a caller must not learn that a clan id
    // they guessed belongs to another user.
    NotFound = 1,

    // at least one supplied person is not one the caller can see
    UnknownPerson = 2,

    // the caller already has a clan by that name
    DuplicateName = 3
}
