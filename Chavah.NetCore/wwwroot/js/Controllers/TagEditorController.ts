﻿namespace BitShuva.Chavah {
    export class TagEditorController {

        static $inject = ["tagApi"];

        allTags: string[] = [];
        selectedTag: string | null = null;
        newTagName = "";
        isSaving = false;

        constructor(
            private readonly tagApi: TagService) {

            this.tagApi.getAll()
                .then(results => this.allTags = results.sort());
        }

        selectTag(tag: string | null) {
            this.newTagName = tag || "";
            this.selectedTag = tag;
        }

        deleteTag(tag: string) {
            if (!this.isSaving) {
                this.isSaving = true;
                this.tagApi.deleteTag(tag)
                    .then(() => {
                        _.pull(this.allTags, tag);
                        this.selectTag(null);
                    })
                    .finally(() => this.isSaving = false);
            }
        }

        renameTag(oldTag: string) {
            let newTagName = this.newTagName;
            if (!this.isSaving && newTagName !== oldTag) {
                this.isSaving = true;
                this.tagApi.renameTag(oldTag, newTagName)
                    .then(result => {
                        let oldTagIndex = this.allTags.indexOf(oldTag);
                        if (oldTagIndex >= 0) {
                            this.allTags[oldTagIndex] = result;
                            this.allTags = _.uniq(this.allTags);
                        }

                        this.selectTag(newTagName);
                    })
                    .finally(() => this.isSaving = false);
            }
        }
    }

    App.controller("TagEditorController", TagEditorController);
}
