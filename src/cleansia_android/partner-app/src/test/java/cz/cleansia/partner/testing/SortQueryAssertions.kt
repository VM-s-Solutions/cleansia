package cz.cleansia.partner.testing

import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals

/**
 * The server binds a paged sort only as these two names at index 0; a bare `Sort`, or any other index,
 * binds an empty list and the page comes back in its default order. → /decisions/adr-0075
 */
fun assertSortedBy(request: RecordedRequest, field: String, direction: Int) {
    val url = checkNotNull(request.requestUrl) { "the request carried no URL" }
    assertEquals(
        "the sort must go out as one key at index 0, in the names the server binds; got ${request.path}",
        setOf("Sort[0].Field", "Sort[0].Direction"),
        url.queryParameterNames.filterTo(mutableSetOf()) { it.startsWith("Sort") },
    )
    assertEquals(listOf(field), url.queryParameterValues("Sort[0].Field"))
    assertEquals(listOf("$direction"), url.queryParameterValues("Sort[0].Direction"))
}
