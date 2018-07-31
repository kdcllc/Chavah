﻿namespace BitShuva.Chavah {
    export class ProfileController {

        static $inject = [
            "accountApi",
            "userApi"
        ];

        user: User;
        profilePicUrl: string | null = null;
        registrationDateAgo: string;
        isSaving = false;
        isUploadingPhoto = false;
        hasSavedSuccessfully = false;
        
        constructor(
            private readonly accountApi: AccountService,
            private readonly userApi: UserApiService) {

            // Make a copy of the user so that we can edit freely without committing.
            this.user = new User(this.accountApi.currentUser!);
            this.profilePicUrl = this.user.profilePicUrl;

            const registrationDate = moment(this.user.registrationDate);
            this.registrationDateAgo = `${registrationDate.fromNow()} (${registrationDate.format('dddd, MMMM Do YYYY, h:mm a')})`;
        }

        get isSavingOrUploading(): boolean {
            return this.isSaving || this.isUploadingPhoto;
        }

        $onInit() {
        }

        launchImagePicker() {
            $("#imagePicker").click();
        }

        async profilePicChanged(e: JQueryEventObject) {
            if (!this.isSaving) {
                const files = e.target["files"] as FileList;
                const file = files.item(0);
                if (file) {
                    this.isUploadingPhoto = true;
                    this.hasSavedSuccessfully = false;
                    try {
                        // Update the profile pic and the profile data.
                        const updatedProfile = await this.userApi.updateProfile(this.user);
                        const updatedProfilePic = await this.userApi.updateProfilePic(file);

                        this.profilePicUrl = updatedProfilePic;
                        this.profileSaved(updatedProfile);

                        this.hasSavedSuccessfully = true;
                    } finally {
                        this.isUploadingPhoto = false;
                    }
                }
            }
        }

        async save() {
            if (!this.isSaving) {
                this.isSaving = true;
                this.hasSavedSuccessfully = false;
                try {
                    const updatedUser = await this.userApi.updateProfile(this.user);
                    this.hasSavedSuccessfully = true;
                    this.profileSaved(updatedUser);
                } finally {
                    this.isSaving = false;
                }
            }
        }

        profileSaved(updatedUser: Server.AppUser) {
            if (this.accountApi.currentUser) {
                this.accountApi.currentUser.updateFrom(updatedUser);
            }
        }
    }

    App.controller("ProfileController", ProfileController);
}