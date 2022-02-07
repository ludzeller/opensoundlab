//
//  CRingBuffer.cpp
//  SoundStageNative
//
//  Created by hb on 19.01.22.
//

#include "CRingBuffer.hpp"
#include "util.h"
#include <assert.h>

#define FB_UNINITIALIZED -1
#define _MAX(a, b) a > b ? a : b
#define _MIN(a, b) a < b ? a : b

void insertFrame(int length, float stride, FrameRingBuffer *x)
{
    assert(length > 0);
    
#if DEBUG
    //printv("Adding %d samples from ptr %d..\n", length, x->ptr);
    vector<FrameHeader> oldFrames(x->headers);
    /*if(FrameRingBuffer_Warn(stride, x) > 1)
    {
        printv("warning: there is already a frame with stride %f\n", stride);
    }*/
#endif
    
    if(length > x->n)
        length = x->n;
    int ptr = x->ptr;
    const int N = x->n;
    FrameHeader *existingFrame;
    FrameHeader newFrame;
    newFrame.length = 0;
    newFrame.head = ptr;
    newFrame.stride = stride;
    
    bool push = true;
    
    //Check if the array contains only 1 frame which already has the correct stride. if so, there is nothing to do.
    existingFrame = &x->headers[0];
    if(x->headers.size() == 1)
    {
        if(existingFrame->stride == newFrame.stride)
        {
            return; //Nothing to change
        }
    }
    //If the oldest frame has the same stride as the new samples, we concatenate them.
    else if(existingFrame->stride == newFrame.stride)
    {
        newFrame.length = existingFrame->length;
        x->headers.erase(x->headers.begin());
    }
    //If the most recent frame has the same stride as the new samples, we concatenate them.
    else
    {
        existingFrame = &x->headers.back();
        if(existingFrame->stride == newFrame.stride)
        {
            //Calculate how many new samples we can store in the existing frame:
            int samplesAvailable = (existingFrame->head + existingFrame->length) - newFrame.head;
            if(newFrame.head < existingFrame->head)
                samplesAvailable -= N;
            newFrame.length = existingFrame->length;
            newFrame.head = existingFrame->head;
            //we have to add the samples that we inherit from the existing frame to our total:
            length += (newFrame.length - samplesAvailable);
            if(length > N)
                length = N;
            x->headers.pop_back();
        }
    }
    
    //At this point we know we will have to modify or delete 1+ existing headers. TODO: But if ptr != header of oldest frame (which can happen through concatenation of 2 frames), then inserting the new frame will modify the head of the oldest frame, and in some cases even split it into 2 frames!
    // before:
    // |--------------------|
    //         ptr
    // after:
    // |-------|-------|----|
    //                  ptr

    //Now we delete headers, consuming their length, until the length of the new frame is identical to the number of samples we want to store.
    while(newFrame.length < length)
    {
        existingFrame = &x->headers[0];
        
        int remaining = length - newFrame.length; //this is only correct if existingFrame->head == ptr, which is not necessarily the case
        //need to consume whole frame, so delete it
        if(existingFrame->length <= remaining)
        {
            newFrame.length += existingFrame->length;
            x->headers.erase(x->headers.begin());
        }
        //no need to consume whole frame, so resize it
        else
        {
            existingFrame->length -= remaining;
            //TODO: if the ptr is between the frame boundaries, the next line is wrong!
            existingFrame->head = (existingFrame->head + remaining) % N;
            newFrame.length = length;
        }
    }
    
    //We check once again if the adjacent frame has the same stride, which could be possible if we deleted some frames and thus "closed a gap" between frames of the same stride. If so, concatenate. We have to leave the frame in its place at the beginning of the vector bc it contains the ptr.
    if(x->headers.size() > 0)
    {
        existingFrame = &x->headers[0];
        if(existingFrame->stride == newFrame.stride)
        {
            existingFrame->length += newFrame.length;
            existingFrame->head = newFrame.head;
            push = false;
        }
    }
    
    if(push)
        x->headers.push_back(newFrame);
    
#if DEBUG
    bool legit = FrameRingBuffer_Validate(x);
    if(!legit)
    {
        printv("Validation failed in InsertFrame with input: \n");
        FrameHeader h;
        for(int i = 0; i < oldFrames.size(); i++)
        {
            h = oldFrames[i];
            printv("%d: start=%d, length=%d, stride=%f\n", i, h.head, h.length, h.stride);
        }
        printv("======\n");
    }
    /*if(FrameRingBuffer_Warn(stride, x) > 1)
    {

    }*/
#endif
}

FrameRingBuffer* FrameRingBuffer_New(int n)
{
    FrameRingBuffer *x = new FrameRingBuffer();
    x->data = (float*)malloc(n * sizeof(float));
    _fZero(x->data, n);
    x->ptr = 0;
    x->n = n;
    x->headers.push_back(FrameHeader());
    x->headers[0].length = n;
    x->headers[0].stride = 1;
    x->headers[0].head = 0;
    
    return x;
}

void FrameRingBuffer_Free(FrameRingBuffer *x)
{
    _free(x->data);
    _free(x);
}

void FrameRingBuffer_GetPtrFrame(int *frame, int *offset, FrameRingBuffer *x)
{
    FrameHeader *f;
    int h, t;
    int N = x->n;
    int ptr = x->ptr;
    for(int i = 0; i < x->headers.size(); i++)
    {
        f = &x->headers[i];
        h = f->head;
        t = (h + f->length) % N;

        if(
        (t == h)
        ||
        (h <= ptr && t >= ptr)
        ||
        (t < h && (ptr >= h || ptr < t))
           )
        {
            *frame = i;
            if(ptr >= h) //TODO: or should it be ptr > h?
                *offset = ptr - h;
            else
                *offset = (ptr + N) - h;
            if(*offset > N)
                printv("%d - %d = %d\n", ptr, h, *offset);
            return;
        }
    }
    printv("ERROR: GetPtrFrame: uncaught case!!!\n");
    FrameRingBuffer_Print(x);
}

/* This is efficient as long as n is reasonably large, e.g. writing buffers of 256 samples. */
void FrameRingBuffer_Write(float* src, int n, float stride, FrameRingBuffer *x)
{
    ///Clamp n
    if(n > x->n)
    {
        src = src + (n - x->n);
        n = x->n;
    }
    
    ///Insert new frame header
    insertFrame(n, stride, x);

    /// Copy data to buffer
    int n1 = _min( x->n - x->ptr, n);
    memcpy(x->data + x->ptr, src, n1*sizeof(float));
    x->ptr = (x->ptr + n1) % x->n;
    if(n1 < n)
    {
        int n2 = n - n1;
        memcpy(x->data, src + n1, n2*sizeof(float));
        x->ptr = (x->ptr + n2) % x->n;
    }
}

void FrameRingBuffer_Read(float* dest, int n, int offset, float stride, FrameRingBuffer *x)
{
    /* Find starting point */
    assert(offset < 0);
    assert(stride > 0);
    assert(abs(offset) <= x->n);
    assert(n <= x->n);
    
    int f;
    int o;
    FrameRingBuffer_GetPtrFrame(&f, &o, x);
    //printv("ptr (%d) is in frame %d with offset %d\n", x->ptr, f, o);
    int headerIndex = f;
    FrameHeader *header = &(x->headers[f]);
    
    float samplesInFrame = o * header->stride / stride;
    float samplesNeeded = -offset - samplesInFrame;
    
    /* Find the position to start reading from */
    while(samplesNeeded > 0)
    {
        headerIndex--;
        if(headerIndex < 0)
            headerIndex = x->headers.size() - 1;
        
        header = &(x->headers[headerIndex]);
        
        samplesInFrame = header->length * header->stride / stride; //number of samples as viewed from the reader's perspective.
        
        samplesNeeded -= samplesInFrame;
    }
    
    /* The position to start reading from is in the current frame with an offset of (-samplesNeeded). */

    int ptr;
    float fPtr = header->head; //the start index of the frame in which the first sample to read is located
    
    //vStride = stride / header->stride;
    samplesInFrame = header->length * header->stride / stride;
    // Calculate the offset from the start of the frame to the first sample to read
    // At this point, samplesNeeded is either 0 or negative; negative meaning we backtracked "too far", so we have to start reading so much into the frame
    samplesNeeded = -samplesNeeded;
    
    for(int i = 0; i < n; i++)
    {
        //Skip some frames if necessary
        while(samplesInFrame < samplesNeeded)
        {
            //assert(i != 0); //in first iteration we should always be in the correct frame
            samplesNeeded -= samplesInFrame;
            headerIndex = (headerIndex + 1) % x->headers.size();
            header = &x->headers[headerIndex];
            //vStride = stride / header->stride;
            samplesInFrame = header->length * header->stride / stride;
            fPtr = header->head;
        }
        
        //we will advance samplesNeeded samples into the frame in order to copy the next sample, so they are not available anymore.
        samplesInFrame -= samplesNeeded;
        
        //We have found the frame in which the next sample to copy is located, so we update our pointers and copy 1 sample to dest.
        fPtr += samplesNeeded * stride / header->stride;
        if(fPtr >= x->n)
            fPtr -= x->n;
        ptr = (int)(fPtr + 0.5f) % x->n;
        //printv("copying: %f\n", x->data[ptr]);
        dest[i] = x->data[ptr];
        
        //we need to advance vStride samples to be able to copy the next sample.
        samplesNeeded = 1;
    }
}

void FrameRingBuffer_Clear(FrameRingBuffer *x)
{
    _fZero(x->data, x->n);
    x->headers.clear();
    FrameHeader h;
    h.length = x->n;
    h.head = 0;
    h.stride = 1;
    x->headers.emplace_back(h);
    x->ptr = 0;
}

int FrameRingBuffer_Warn(float stride, FrameRingBuffer *x)
{
    int count = 0;
    for(int i = 0; i < x->headers.size(); i++)
    {
        if(x->headers[i].stride == stride)
            count++;
    }
    return count;
}

bool FrameRingBuffer_Validate(FrameRingBuffer *x)
{
    bool legit = true;
    
    int numSmpls = 0;
    for(int i = 0; i < x->headers.size(); i++)
        numSmpls += x->headers[i].length;
    if(numSmpls != x->n)
    {
        printv("VALIDATION FAILED: the buffer contains %d samples, but the headers sum up to %d samples: \n", x->n, numSmpls);
        legit = false;
    }
    
    int adjacentFramesWithSameStride = 0;
    if(x->headers.size() > 1)
    {
        for(int i = 0; i < x->headers.size(); i++)
        {
            float stride1 = x->headers[i].stride;
            float stride2 = x->headers[(i + 1) % x->headers.size()].stride;
            if (stride1 == stride2)
                adjacentFramesWithSameStride++;
        }
    }

    //assert(adjacentFramesWithSameStride == 0);
    if(adjacentFramesWithSameStride != 0)
    {
        printv("VALIDATION FAILED: the buffer contains %d adjacent frames with same stride: \n", adjacentFramesWithSameStride);
        legit = false;
    }
    
    int corruptCounds = 0;
    for(int i = 0; i < x->headers.size(); i++)
    {
        int tail = (x->headers[i].head + x->headers[i].length) % x->n;
        int nextHead = x->headers[(i+1) % x->headers.size()].head;
        if(tail != nextHead)
        {
            corruptCounds++;
        }
    }
    if(corruptCounds != 0)
    {
        printv("VALIDATION FAILED: the buffer contains %d corrupt bounds. \n", corruptCounds);
        legit = false;
    }
        
    
    if(!legit)
        FrameRingBuffer_Print(x);
        
    return legit;
}

void FrameRingBuffer_Print(FrameRingBuffer *x)
{
    printv("ptr: %d / %d\n", x->ptr, x->n);
    printv("======\n");
    printv("HEADERS:\n");
    FrameHeader h;
    for(int i = 0; i < x->headers.size(); i++)
    {
        h = x->headers[i];
        printv("%d: start=%d, length=%d, stride=%f\n", i, h.head, h.length, h.stride);
    }
    printv("======\n");
    /*printv("BUFFER:\n");
    for(int i = 0; i < x->n; i++)
    {
        printv("%d: %f\n", i, x->data[i]);
    }*/
}
