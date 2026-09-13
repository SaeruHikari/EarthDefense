#include <stdlib.h>
#include <errno.h>
__declspec(dllexport) float skrgui_strtof(const char* text, char** end, int* range_error) {
 errno=0; float result=strtof(text,end); *range_error=(errno==ERANGE);return result;
}
