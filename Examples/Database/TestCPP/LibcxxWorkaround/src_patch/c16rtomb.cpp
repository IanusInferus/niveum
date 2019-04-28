#include <cuchar>
#include <errno.h>
#include <wchar.h>

_LIBCPP_BEGIN_NAMESPACE_STD

using ::mbstate_t;
using ::size_t;

size_t c16rtomb(char * s, char16_t c16, mbstate_t * ps)
{
	static_assert(sizeof(mbstate_t) >= sizeof(unsigned));
	static unsigned internal_state;
	if (!ps) ps = (mbstate_t *)(void *)&internal_state;
	unsigned *x = (unsigned *)ps;
	wchar_t wc;

	if (!s) {
		if (*x) goto ilseq;
		return 1;
	}

	if (!*x && c16 - 0xd800u < 0x400) {
		*x = (c16 - 0xd7c0) << 10;
		return 0;
	}

	if (*x) {
		if (c16 - 0xdc00u >= 0x400) goto ilseq;
		else wc = *x + c16 - 0xdc00;
		*x = 0;
	} else {
		wc = c16;
	}
	return wcrtomb(s, wc, 0);

ilseq:
	*x = 0;
	errno = EILSEQ;
	return -1;
}

_LIBCPP_END_NAMESPACE_STD
