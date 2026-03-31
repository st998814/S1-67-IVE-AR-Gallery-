var webGlNativeFileBrowserLibrary = {

    initialize : function(version){
        document.callFBFunctionByName("initializeFBLibrary", null);

        if(typeof UTF8ToString === "function"){
            document.convertPtrToString = UTF8ToString;
        } else{
            document.convertPtrToString = Pointer_stringify;
        }
    },

    openFileBrowserForLoad: function(typesFilter, isMultipleSelection, isFolder){    
        var data = [ document.convertPtrToString(typesFilter), isMultipleSelection, isFolder ];

        document.callFBFunctionByName("openFileBrowserForLoad", data);
    },
    
    closeFileBrowserForOpen: function(){
        document.callFBFunctionByName("closeFileBrowserForOpen", null);
    },

    saveFile: function(fileName, data){
        document.callFBFunctionByName("saveFile", {
                name: document.convertPtrToString(fileName),
                data: document.convertPtrToString(data)
            });  
    },

    setLocalization: function(key, value){
        document.callFBFunctionByName("setLocalization", {
            key: document.convertPtrToString(key),
            value: document.convertPtrToString(value)
        });
    },

    cleanup: function(){
        if(document.fbStorage == null || document.fbStorage.initialized !== true)
            return;

        for(var i = 0; i < document.fbStorage.dataPointers.length; i++){
            _free(document.fbStorage.dataPointers[i]);
        }
        document.callFBFunctionByName("cleanupFB", null);
    },

    loadFileData: function(fileName){
        if(document.fbStorage == null || document.fbStorage.initialized !== true)
            return null;

        var file = document.fbStorage.loadedFiles[document.convertPtrToString(fileName)];
        var dataPointer = _malloc(file.info.byteLength);
        var dataHeap = new Uint8Array(HEAPU8.buffer, dataPointer, file.info.byteLength);
        dataHeap.set(new Uint8Array(file.info));
        document.fbStorage.dataPointers.push(dataPointer);
        return dataPointer;
    }
};

mergeInto(LibraryManager.library, webGlNativeFileBrowserLibrary);